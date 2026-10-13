# MET compatibility patch

Source: XCharts 3.15.0, official `v3.15.0` tag at https://github.com/XCharts-Team/XCharts, MIT license retained in LICENSE.md. Examples and documentation are excluded from the embedded runtime package.

Unity 6000.6.5f1 treats `Object.GetInstanceID()` as an obsolete compile error. `Runtime/Internal/Pools/SerieLabelPool.cs` uses the supported `GetEntityId()` and `Dictionary<EntityId, bool>` for the same released-label identity bookkeeping. No chart data or plotting algorithm changes.
