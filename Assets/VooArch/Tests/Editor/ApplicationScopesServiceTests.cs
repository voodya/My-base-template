#if HAS_VOODYA
using System;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voodya.Core;

namespace Voodya.Core.Tests
{
    public class ApplicationScopesServiceTests
    {
        private class StateA : ABaseEntryPoint { public StateA() : base(null, null, null) { } }
        private class StateB : ABaseEntryPoint { public StateB() : base(null, null, null) { } }
        private class StateC : ABaseEntryPoint { public StateC() : base(null, null, null) { } }

        /// <summary>State without scope and scenes, every stage can be held or failed by a test</summary>
        private class FakeState : IABaseState
        {
            public int Entered;
            public int Started;
            public int Exited;
            public object LastPayload;
            public Exception EnterError;
            public Exception StartError;
            public UniTaskCompletionSource StartGate;
            public UniTaskCompletionSource ExitGate;
            public Func<UniTask> OnStart;

            public FakeState(Type type) => CurrentType = type;

            public Type CurrentType { get; }
            public bool IsActive { get; private set; }

            public UniTask Enter(object payload = null)
            {
                if (EnterError != null) throw EnterError;
                Entered++;
                LastPayload = payload;
                IsActive = true;
                return UniTask.CompletedTask;
            }

            public async UniTask Start()
            {
                Started++;
                if (StartError != null) throw StartError;
                if (StartGate != null) await StartGate.Task;
                if (OnStart != null) await OnStart();
            }

            public async UniTask Exit()
            {
                if (!IsActive) return;
                // Like DefaultState: exit cancels the start that is still running
                StartGate?.TrySetCanceled();
                if (ExitGate != null) await ExitGate.Task;
                IsActive = false;
                Exited++;
            }
        }

        private ApplicationScopesService _service;
        private FakeState _a;
        private FakeState _b;
        private FakeState _c;

        [SetUp]
        public void SetUp()
        {
            _service = new ApplicationScopesService();
            _a = new FakeState(typeof(StateA));
            _b = new FakeState(typeof(StateB));
            _c = new FakeState(typeof(StateC));
            _service.Register(_a);
            _service.Register(_b);
            _service.Register(_c);
        }

        private static T Result<T>(UniTask<T> task)
        {
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, "Task is expected to be completed");
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void ChangeScope_StartsStateAndExitsPrevious()
        {
            Assert.IsTrue(Result(_service.TryChangeScope<StateA>()));
            Assert.IsTrue(Result(_service.TryChangeScope<StateB>()));

            Assert.AreEqual(1, _a.Exited);
            Assert.AreEqual(1, _b.Started);
            Assert.AreEqual(typeof(StateB), _service.CurrentState);
            Assert.IsFalse(_service.IsBusy);
        }

        [Test]
        public void ChangeScope_IsRejectedWhileTransitioning()
        {
            Result(_service.TryChangeScope<StateA>());
            _a.ExitGate = new UniTaskCompletionSource();

            var first = _service.TryChangeScope<StateB>();
            Assert.IsTrue(_service.IsTransitioning);
            Assert.IsFalse(Result(_service.TryChangeScope<StateC>()));

            _a.ExitGate.TrySetResult();
            Assert.IsTrue(Result(first));
            Assert.AreEqual(typeof(StateB), _service.CurrentState);
            Assert.AreEqual(0, _c.Entered);
        }

        [Test]
        public void StartingState_CanOpenAdditionalState()
        {
            _a.OnStart = async () => Assert.IsTrue(await _service.TryAddScope<StateB>());

            Assert.IsTrue(Result(_service.TryChangeScope<StateA>()));
            Assert.IsTrue(_b.IsActive);
        }

        [Test]
        public void ChangeScope_ClosesAdditionalStates()
        {
            Result(_service.TryChangeScope<StateA>());
            Result(_service.TryAddScope<StateB>());

            Assert.IsTrue(Result(_service.TryChangeScope<StateC>()));
            Assert.AreEqual(1, _b.Exited);
            Assert.IsFalse(_b.IsActive);
        }

        [Test]
        public void CloseAdditionalScope_AllowsToOpenItAgain()
        {
            Result(_service.TryChangeScope<StateA>());
            Result(_service.TryAddScope<StateB>());

            Assert.IsTrue(Result(_service.CloseAdditionalScope<StateB>()));
            Assert.AreEqual(1, _b.Exited);
            Assert.IsTrue(Result(_service.TryAddScope<StateB>()));
            Assert.AreEqual(2, _b.Entered);
        }

        [Test]
        public void AddScope_WithoutCurrentState_Fails()
        {
            LogAssert.Expect(LogType.Error, new Regex("no current state"));
            Assert.IsFalse(Result(_service.TryAddScope<StateB>()));
            Assert.AreEqual(0, _b.Entered);
        }

        [Test]
        public void FailedStart_ReturnsToHomeState()
        {
            Result(_service.TryChangeScope<StateA>());
            _b.StartError = new InvalidOperationException("start failed");
            Type failedState = null;
            _service.OnStateFailed += (type, _) => failedState = type;

            LogAssert.Expect(LogType.Exception, new Regex("start failed"));
            Assert.IsFalse(Result(_service.TryChangeScope<StateB>()));

            Assert.AreEqual(typeof(StateB), failedState);
            Assert.AreEqual(typeof(StateA), _service.CurrentState);
            Assert.AreEqual(1, _b.Exited);
            Assert.AreEqual(2, _a.Entered);
        }

        [Test]
        public void FailedEnter_ReturnsToHomeState()
        {
            Result(_service.TryChangeScope<StateA>());
            _b.EnterError = new InvalidOperationException("enter failed");

            LogAssert.Expect(LogType.Exception, new Regex("enter failed"));
            Assert.IsFalse(Result(_service.TryChangeScope<StateB>()));

            Assert.AreEqual(typeof(StateA), _service.CurrentState);
            Assert.AreEqual(2, _a.Entered);
        }

        [Test]
        public void ReplacedStartingState_IsCancelledWithoutFallback()
        {
            Result(_service.TryChangeScope<StateA>());
            _b.StartGate = new UniTaskCompletionSource();

            var changeToB = _service.TryChangeScope<StateB>();
            Assert.IsFalse(_service.IsTransitioning, "Lock is released while the state starts");
            Assert.IsTrue(_service.IsBusy);

            Assert.IsTrue(Result(_service.TryChangeScope<StateC>()));
            Assert.IsFalse(Result(changeToB));
            Assert.AreEqual(typeof(StateC), _service.CurrentState);
            Assert.AreEqual(1, _a.Entered, "Cancelled start must not return to the home state");
        }

        [Test]
        public void Payload_IsPassedToState()
        {
            var payload = new object();
            Result(_service.TryChangeScope<StateA>(payload));
            Assert.AreSame(payload, _a.LastPayload);
        }
    }
}
#endif
