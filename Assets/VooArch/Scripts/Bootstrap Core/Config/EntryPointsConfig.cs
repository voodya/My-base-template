using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PreserveAttribute = UnityEngine.Scripting.PreserveAttribute;
#if HAS_VOODYA
using VContainer;
#endif

#if HAS_VOODYA
namespace Voodya.Core
{
    /// <summary>
    /// Entry points registered as application states, stored as type names.
    /// Filled automatically in the editor from classes with [EntryPoint] attribute,
    /// so removing or renaming an entry point never breaks compilation.
    /// The first entry is the start state.
    /// </summary>
    [CreateAssetMenu(fileName = "EntrysMapping", menuName = "VooArch/Entry Points Config")]
    public class EntryPointsConfig : ScriptableStates
    {
        [SerializeField] private List<string> _entryPoints = new();

        public IReadOnlyList<string> EntryPoints => _entryPoints;

        public override void OnRegister(IApplicationScopesService applicationScopesService, IObjectResolver objectResolver)
        {
            _firstStateType = null;
            foreach (var typeName in _entryPoints)
            {
                var type = Type.GetType(typeName);
                if (type == null || type.IsAbstract || !typeof(ABaseEntryPoint).IsAssignableFrom(type))
                {
                    VooLog.Error($"Entry point '{typeName}' not found. Open VooArch/Voo arch configure to sync config.");
                    continue;
                }
                var stateType = typeof(DefaultState<>).MakeGenericType(type);
                applicationScopesService.Register((IABaseState)Activator.CreateInstance(stateType, objectResolver));
                _firstStateType ??= type;
            }
        }

#if UNITY_EDITOR
        /// <returns>true if the list has changed</returns>
        public bool EditorSetEntryPoints(List<string> entryPoints)
        {
            if (_entryPoints.SequenceEqual(entryPoints)) return false;
            _entryPoints = entryPoints;
            return true;
        }
#endif

        // Keeps shared generic code of DefaultState<> for IL2CPP, MakeGenericType relies on it. Never called.
        [Preserve]
        private static void AotHint() => _ = new DefaultState<ABaseEntryPoint>(null);
    }
}
#endif
