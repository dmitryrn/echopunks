#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using HarmonyLib;

namespace Echopunks.Dev
{
    // Capture the game's own displayed goal tables without reading puzzle internals.
    // The table and show-goal hooks mirror the accessibility module's PanelCapture.
    internal static class GogGoalCapture
    {
        private static bool _installed, _armed;
        private static object _editor;
        private static int _frames;
        private static string _status = "idle", _error;
        private static readonly List<object> Tables = new List<object>();
        private static FieldInfo _header, _values;
        private static MethodInfo _valueText;

        internal static string Start()
        {
            try
            {
                GogState.EnsureCompatible();
                object editor = GameState.TopScreen();
                if (editor == null || editor.GetType().Name != "EditorScreen")
                    return Json(new { ok = false, error = "Open a puzzle editor first." });
                if (_armed) return Json(new { ok = false, error = "Goal capture is already pending." });
                Install();
                Tables.Clear();
                _error = null;
                _editor = editor;
                _frames = 0;
                _status = "pending";
                _armed = true;
                return Json(new { ok = true, status = _status });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string Status()
        {
            return Json(new { ok = _status != "error", status = _status, error = _error,
                tables = _status == "completed" ? Tables.ToArray() : null });
        }

        internal static void Tick()
        {
            if (!_armed) return;
            if (!ReferenceEquals(GameState.TopScreen(), _editor))
            {
                _armed = false;
                _status = "error";
                _error = "The editor changed before the goal was captured.";
                return;
            }
            // Two complete draw frames allow the game's goal view to update.
            if (++_frames >= 3)
            {
                _armed = false;
                _status = _error == null ? "completed" : "error";
            }
        }

        private static void Install()
        {
            if (_installed) return;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type editor = GogState.ResolveType("EditorScreen");
            Type logic = GogState.ResolveType("SpecialPuzzleLogics");
            MethodInfo goal = editor.GetMethods(flags).Single(m =>
                m.Name == GogState.Name("M", "EditorScreen", "method_7") && m.GetParameters().Length == 0);
            MethodInfo table = logic.GetMethods(flags).Single(m =>
                m.Name == GogState.Name("M", "SpecialPuzzleLogics", "smethod_2") &&
                m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.IsArray);
            Type column = table.GetParameters()[1].ParameterType.GetElementType();
            _header = column.GetField(GogState.Name("F", "SpecialPuzzleLogics/TableColumn", "locString_0"), flags);
            _values = column.GetField(GogState.Name("F", "SpecialPuzzleLogics/TableColumn", "exaValue_0"), flags);
            Type value = GogState.ResolveType("ExaValue");
            _valueText = value.GetMethods(flags).Single(m =>
                m.Name == GogState.Name("M", "ExaValue", "method_2") && m.GetParameters().Length == 1);
            if (_header == null || _values == null) throw new InvalidOperationException("Goal table fields are unavailable.");
            var harmony = new Harmony("exa.bridge.goal-capture");
            harmony.Patch(goal, prefix: new HarmonyMethod(typeof(GogGoalCapture), nameof(ShowGoal)));
            harmony.Patch(table, prefix: new HarmonyMethod(typeof(GogGoalCapture), nameof(CaptureTable)));
            _installed = true;
        }

        private static bool ShowGoal(ref bool __result)
        {
            if (!_armed) return true;
            __result = true;
            return false;
        }

        private static void CaptureTable(object[] __args)
        {
            if (!_armed || Tables.Count >= 32) return;
            try
            {
                var columns = __args[1] as Array;
                if (columns == null) return;
                var result = new List<object>();
                foreach (object column in columns)
                {
                    if (column == null) continue;
                    var values = new List<string>();
                    var entries = _values.GetValue(column) as Array;
                    if (entries != null)
                        foreach (object entry in entries)
                            values.Add(Convert.ToString(_valueText.Invoke(entry, new object[] { true })));
                    result.Add(new { header = Convert.ToString(_header.GetValue(column)), values });
                }
                // Replace duplicate per-frame captures while keeping distinct tables.
                var table = new { columns = result };
                string signature = Json(table);
                if (!Tables.Any(existing => Json(existing) == signature)) Tables.Add(table);
            }
            catch (Exception e) { _error = e.GetBaseException().Message; }
        }

        private static string Json(object value) => new JavaScriptSerializer().Serialize(value);
    }
}
#endif
