#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Echopunks.Dev
{
    // Read-only experiment for the exact GOG executable inspected in this workspace.
    // Generated names are build-specific; never guess if the binary or mapping differs.
    internal static class GogState
    {
        private const string ExpectedHash = "9e32c95075421faa26fdbeabb97e70f89f398908ee6eecfb9bf679107e43420f";
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static Dictionary<string, string> _map;
        private static bool _verified;
        private static object _pendingEditor;
        private static int _pendingCycle;
        private static object _runEditor;
        private static string _runState = "idle";
        private static string _runReason;
        private static System.Diagnostics.Stopwatch _runClock;
        private static int _runLimitSeconds;
        private static string _runPuzzleId;
        private static BattleResult _battleResult;

        private sealed class BattleResult
        {
            public int wins { get; set; }
            public int draws { get; set; }
            public int losses { get; set; }
            public bool won { get; set; }
            public bool lost { get; set; }
            public string outcome { get; set; }
        }

        private static BattleResult ReadBattleResult(object screen)
        {
            bool won = (bool)Field(screen, "BattleCompletionScreen", "bool_0");
            bool lost = (bool)Field(screen, "BattleCompletionScreen", "bool_1");
            return new BattleResult {
                wins = (int)Field(screen, "BattleCompletionScreen", "int_0"),
                draws = (int)Field(screen, "BattleCompletionScreen", "int_1"),
                losses = (int)Field(screen, "BattleCompletionScreen", "int_2"),
                won = won, lost = lost,
                outcome = won ? "won" : lost ? "lost" : "drawn"
            };
        }

        // Called from the game thread each frame. The bound remains effective
        // even if the HTTP client disconnects.
        internal static void Tick()
        {
            GogGoalCapture.Tick();
            if (_runState != "running" || _runEditor == null) return;
            try
            {
                object top = GameState.TopScreen();
                if (top != null && top.GetType().Name == "PuzzleCompletionScreen" &&
                    ReferenceEquals(Field(top, "PuzzleCompletionScreen", "editorScreen_0"), _runEditor))
                {
                    _runState = "completed";
                    _runReason = "Game displayed the completion screen after its test sequence.";
                }
                else if (top != null && top.GetType().Name == "BattleCompletionScreen" &&
                    ReferenceEquals(Field(top, "BattleCompletionScreen", "editorScreen_0"), _runEditor))
                {
                    _battleResult = ReadBattleResult(top);
                    _runState = "completed";
                    _runReason = "Game displayed the battle results screen.";
                }
                else if (!ReferenceEquals(top, _runEditor))
                {
                    _runState = "interrupted";
                    _runReason = "The active game screen changed.";
                }
                else if (_runClock.Elapsed.TotalSeconds >= _runLimitSeconds)
                {
                    Invoke(_runEditor, "EditorScreen", "method_20");
                    _runState = "timed_out";
                    _runReason = "Time limit reached; pause requested.";
                }
            }
            catch (Exception e)
            {
                // Fail closed: make a best-effort pause if status checks fail.
                try { Invoke(_runEditor, "EditorScreen", "method_20"); } catch { }
                _runState = "error";
                _runReason = e.GetBaseException().Message;
            }
        }

        internal static string RunAll(string body)
        {
            try
            {
                EnsureCompatible();
                object editor = Editor();
                if (_runState == "running")
                    return Json(new { ok = false, error = "A run is already in progress." });
                int seconds = 30;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var input = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                    if (input != null && input.ContainsKey("timeout_seconds"))
                    {
                        object value = input["timeout_seconds"];
                        if (!(value is int)) return Json(new { ok = false, error = "timeout_seconds must be an integer." });
                        seconds = (int)value;
                    }
                }
                if (seconds < 1 || seconds > 120)
                    return Json(new { ok = false, error = "timeout_seconds must be between 1 and 120." });
                if (ReferenceEquals(_pendingEditor, editor))
                    return Json(new { ok = false, error = "Wait for the pending step before starting a run." });
                // method_26 is the game's own expanded-code compile check.
                string checkName = Name("M", "EditorScreen", "method_26");
                var check = editor.GetType().GetMethods(Methods)
                    .Single(m => m.Name == checkName && m.GetParameters().Length == 3);
                object[] checkArgs = { 1, 0, false };
                check.Invoke(editor, checkArgs);
                if ((bool)checkArgs[2])
                    return Json(new { ok = false, error = "The game reports compile errors; fix the code before running." });
                object solution = Field(editor, "EditorScreen", "solution_0");
                object puzzle = Invoke(solution, "Solution", "method_0");
                var id = puzzle.GetType().GetField("ID", Fields);
                if (id == null) throw new MissingFieldException("Puzzle", "ID");
                string name = Name("M", "EditorScreen", "method_18");
                var start = editor.GetType().GetMethods(Methods)
                    .Single(m => m.Name == name && m.GetParameters().Length == 2);
                object noStepBudget = Activator.CreateInstance(start.GetParameters()[1].ParameterType);
                start.Invoke(editor, new[] { (object)true, noStepBudget });
                _pendingEditor = null;
                _runEditor = editor;
                _runPuzzleId = (string)id.GetValue(puzzle);
                _runLimitSeconds = seconds;
                _runReason = null;
                _battleResult = null;
                _runClock = System.Diagnostics.Stopwatch.StartNew();
                _runState = "running";
                return Json(new { ok = true, status = _runState, puzzle_id = _runPuzzleId, timeout_seconds = seconds });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string RunStatus()
        {
            try
            {
                Tick();
                int? testIndex = null;
                int? completedTests = null;
                int? cycle = null;
                object scores = null;
                object testRuns = null;
                object histograms = null;
                int? sizeLimit = null;
                bool? sizeLimitExceeded = null;
                string scoreEligibility = "not_available";
                string eligibilityWarning = null;
                if (_runEditor != null)
                {
                    object solution = Field(_runEditor, "EditorScreen", "solution_0");
                    object puzzle = Invoke(solution, "Solution", "method_0");
                    sizeLimit = (int)Field(PuzzleInfo(puzzle), "GClass202", "int_2");
                    testIndex = (int)Field(_runEditor, "EditorScreen", "int_0") + 1;
                    completedTests = ((ICollection)Field(_runEditor, "EditorScreen", "list_0")).Count;
                    object sim = Field(_runEditor, "EditorScreen", "sim_0");
                    if (sim != null) cycle = (int)Invoke(sim, "Sim", "method_52");
                    if (_runState == "completed" && _battleResult != null)
                        completedTests = _battleResult.wins + _battleResult.draws + _battleResult.losses;
                    if (_runState == "completed" && _battleResult == null)
                    {
                        var values = new Dictionary<string, object>();
                        foreach (DictionaryEntry entry in (IDictionary)Field(solution, "Solution", "dictionary_0"))
                            values[Convert.ToString(entry.Key).ToLowerInvariant()] = entry.Value;
                        scores = values;
                        object size;
                        if (values.TryGetValue("size", out size))
                        {
                            sizeLimitExceeded = Convert.ToInt32(size) > sizeLimit.Value;
                            scoreEligibility = sizeLimitExceeded.Value ? "ineligible_size" : "size_limit_met";
                            eligibilityWarning = sizeLimitExceeded.Value
                                ? "Task completed, but the solution exceeds the size limit; scores are not eligible for histograms or leaderboards."
                                : "The size limit is met; other eligibility conditions are not verified.";
                        }
                        var runs = new List<object>();
                        foreach (DictionaryEntry entry in (IDictionary)Field(_runEditor, "EditorScreen", "dictionary_0"))
                        {
                            var data = (Tuple<int, int>)entry.Value;
                            runs.Add(new { test = (int)entry.Key + 1, cycles = data.Item1, activity = data.Item2 });
                        }
                        testRuns = runs.OrderBy(r => (int)r.GetType().GetProperty("test").GetValue(r)).ToArray();
                        object top = GameState.TopScreen();
                        if (top != null && top.GetType().Name == "PuzzleCompletionScreen" &&
                            ReferenceEquals(Field(top, "PuzzleCompletionScreen", "editorScreen_0"), _runEditor))
                        {
                            var charts = new Dictionary<string, object>();
                            foreach (DictionaryEntry entry in (IDictionary)Field(top, "PuzzleCompletionScreen", "dictionary_0"))
                            {
                                object chart = entry.Value;
                                object histogram = Optional(Field(chart, "GClass2", "maybe_0"));
                                charts[Convert.ToString(entry.Key).ToLowerInvariant()] = new {
                                    previous_best = Optional(Field(chart, "GClass2", "maybe_1")),
                                    previous_solution_score = Optional(Field(chart, "GClass2", "maybe_2")),
                                    current_score = Optional(Field(chart, "GClass2", "maybe_3")),
                                    histogram = histogram == null ? null : new {
                                        axis_max = Field(histogram, "GClass23", "int_0"),
                                        normalized_bins = Field(histogram, "GClass23", "float_0")
                                    }
                                };
                            }
                            histograms = charts;
                        }
                    }
                }
                return Json(new { ok = true, status = _runState, puzzle_id = _runPuzzleId,
                    reason = _runReason, test_index = testIndex, tests_completed = completedTests,
                    task_completed = _runState == "completed" && (_battleResult == null || _battleResult.won),
                    battle_result = _battleResult, cycle, scores,
                    size_limit = sizeLimit, size_limit_exceeded = sizeLimitExceeded,
                    score_eligibility = scoreEligibility, eligibility_warning = eligibilityWarning,
                    test_runs = testRuns, histograms,
                    elapsed_seconds = _runClock == null ? 0 : (int)_runClock.Elapsed.TotalSeconds });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string PauseRun()
        {
            try
            {
                if (_runState != "running" || _runEditor == null)
                    return Json(new { ok = false, error = "No bridge run is active." });
                if (!ReferenceEquals(GameState.TopScreen(), _runEditor))
                {
                    Tick();
                    return Json(new { ok = false, error = "Run is no longer on the editor screen." });
                }
                Invoke(_runEditor, "EditorScreen", "method_20");
                _runState = "paused";
                _runReason = "Pause requested.";
                return Json(new { ok = true, status = _runState });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string OpenDesktop()
        {
            try
            {
                EnsureCompatible();
                object screen = GameState.TopScreen();
                if (screen == null) return Json(new { ok = false, error = "Game screen is not ready." });
                Type desktopType = ResolveType("DesktopScreen");
                if (desktopType.IsInstanceOfType(screen))
                    return Json(new { ok = true, message = "Desktop already open." });
                if (!ResolveType("GClass37").IsInstanceOfType(screen))
                    return Json(new { ok = false, error = "Return to the title room before opening the desktop." });
                object desktop = Activator.CreateInstance(desktopType);
                Invoke(GameState.Instance, "GameLogic", "method_12", desktop);
                return Json(new { ok = true, message = "Desktop transition requested." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string ListTasks()
        {
            try
            {
                EnsureCompatible();
                RequireNormalProgression();
                object desktop = Desktop();
                var tasks = new List<object>();
                foreach (object item in CampaignItems())
                {
                    // Follow the actual desktop visibility markers. Do not disclose hidden tasks.
                    if (!(bool)Invoke(desktop, "DesktopScreen", "method_6", item)) continue;
                    tasks.Add(new
                    {
                        id = Field(item, "CampaignItem", "string_0"),
                        title = Convert.ToString(Field(item, "CampaignItem", "locString_0")),
                        subtitle = Convert.ToString(Field(item, "CampaignItem", "locString_1")),
                        description = Convert.ToString(Field(item, "CampaignItem", "locString_2")),
                        kind = Convert.ToInt32(Field(item, "CampaignItem", "genum1_0")),
                        completed = Invoke(item, "CampaignItem", "method_0")
                    });
                }
                return Json(new { ok = true, tasks });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string OpenTask(string body)
        {
            try
            {
                EnsureCompatible();
                RequireNormalProgression();
                var input = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                if (input == null || !input.ContainsKey("id") || !(input["id"] is string))
                    return Json(new { ok = false, error = "Expected JSON with a task id." });
                string id = (string)input["id"];
                object desktop = Desktop();
                foreach (object item in CampaignItems())
                {
                    if (!string.Equals(Field(item, "CampaignItem", "string_0") as string, id, StringComparison.Ordinal))
                        continue;
                    if (!(bool)Invoke(desktop, "DesktopScreen", "method_6", item))
                        return Json(new { ok = false, error = "Task not found or not unlocked." });
                    // These are the same selection and activation paths as a desktop click.
                    Invoke(desktop, "DesktopScreen", "method_9", item);
                    Invoke(desktop, "DesktopScreen", "method_11");
                    return Json(new { ok = true, message = "Task selected; game transition requested.", id });
                }
                return Json(new { ok = false, error = "Task not found or not unlocked." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        private static object Desktop()
        {
            object screen = GameState.TopScreen();
            if (screen == null || screen.GetType().Name != "DesktopScreen")
                throw new InvalidOperationException("Open the game desktop first.");
            return screen;
        }

        private static void RequireNormalProgression()
        {
            // DesktopScreen.method_6 also returns true for the game's show-all
            // developer flag. Refuse to list or open while that override is set.
            Type globals = ResolveType("GClass80");
            FieldInfo flag = globals.GetField(Name("F", "GClass80", "bool_5"),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (flag == null || flag.FieldType != typeof(bool))
                throw new InvalidOperationException("Cannot verify the game's progression override.");
            if ((bool)flag.GetValue(null))
                throw new InvalidOperationException("Game's show-all override is enabled; progression cannot be verified.");
        }

        private static IEnumerable CampaignItems()
        {
            Type campaign = ResolveType("GClass65");
            foreach (string name in new[] { "gclass156_0", "gclass156_1" })
            {
                if (name == "gclass156_1")
                {
                    // The desktop hides its side-task tab until this story flag is set.
                    object save = Field(GameState.Instance, "GameLogic", "saveData_0");
                    if (!(bool)Invoke(save, "SaveData", "method_13", "ember-7", 0)) continue;
                }
                FieldInfo field = campaign.GetField(Name("F", "GClass65", name),
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) throw new MissingFieldException("GClass65", name);
                object group = field.GetValue(null);
                if (group == null) throw new InvalidOperationException("Campaign is not initialized.");
                foreach (object item in (IEnumerable)Field(group, "GClass156", "list_0"))
                    yield return item;
            }
        }

        internal static string SetCode(string body)
        {
            try
            {
                EnsureCompatible();
                if (_runState == "running")
                    return Json(new { ok = false, error = "Pause the active run before changing code." });
                var input = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                if (input == null || !input.ContainsKey("code") || !(input["code"] is string))
                    return Json(new { ok = false, error = "Expected JSON with a code string." });
                string code = (string)input["code"];
                if (code.Length > 24000) return Json(new { ok = false, error = "Code is too long." });
                object editor = Editor();
                if (!(bool)Invoke(editor, "EditorScreen", "method_2"))
                    return Json(new { ok = false, error = "Reset the simulation before editing." });
                object solution = Field(editor, "EditorScreen", "solution_0");
                var programs = (IList)Field(solution, "Solution", "list_0");
                if (programs.Count == 0) return Json(new { ok = false, error = "No initial EXA." });
                var field = programs[0].GetType().GetField(Name("F", "SolutionExa", "string_1"), Fields);
                if (field == null) throw new MissingFieldException("SolutionExa", "string_1");
                field.SetValue(programs[0], code.Replace("\r\n", "\n"));
                Invoke(editor, "EditorScreen", "method_32");
                return Json(new { ok = true, message = "Code submitted for first EXA; game compiles on the next frame." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string ListExas()
        {
            try
            {
                EnsureCompatible();
                object editor = Editor();
                object solution = Field(editor, "EditorScreen", "solution_0");
                var exas = new List<object>();
                foreach (object exa in (IEnumerable)Field(solution, "Solution", "list_0"))
                    exas.Add(new {
                        name = Field(exa, "SolutionExa", "string_0"),
                        code = Field(exa, "SolutionExa", "string_1")
                    });
                return Json(new { ok = true, exas });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string EditExas(string method, string body)
        {
            try
            {
                EnsureCompatible();
                if (_runState == "running")
                    return Json(new { ok = false, error = "Pause the active run before changing EXAs." });
                var input = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                if (input == null) return Json(new { ok = false, error = "Expected a JSON object." });
                string code = null;
                if (method != "DELETE")
                {
                    if (!input.ContainsKey("code") || !(input["code"] is string) || ((string)input["code"]).Length > 24000)
                        return Json(new { ok = false, error = "Expected a code string of at most 24000 characters." });
                    code = ((string)input["code"]).Replace("\r\n", "\n");
                }
                string name = null;
                if (method != "POST")
                {
                    if (!input.ContainsKey("name") || !(input["name"] is string) || string.IsNullOrWhiteSpace((string)input["name"]))
                        return Json(new { ok = false, error = "Expected an existing EXA name." });
                    name = (string)input["name"];
                }
                object editor = Editor();
                if (!(bool)Invoke(editor, "EditorScreen", "method_2"))
                    return Json(new { ok = false, error = "Reset the simulation before editing." });
                object solution = Field(editor, "EditorScreen", "solution_0");
                var programs = (IList)Field(solution, "Solution", "list_0");
                object exa = null;
                if (method == "POST")
                {
                    if (programs.Count >= 26)
                        return Json(new { ok = false, error = "The bridge supports at most 26 initial EXAs." });
                    object sim = Field(editor, "EditorScreen", "sim_0");
                    object team = Invoke(editor, "EditorScreen", "method_24");
                    object teamInfo = ((IDictionary)Field(sim, "Sim", "dictionary_0"))[team];
                    if (teamInfo == null || (bool)Field(sim, "Sim", "bool_4") ||
                        programs.Count >= (int)Field(teamInfo, "Sim/GClass391", "int_0") ||
                        programs.Count >= (int)Field(sim, "Sim", "int_6"))
                        return Json(new { ok = false, error = "The editor does not allow another initial EXA here." });
                    object home = Field(teamInfo, "Sim/GClass391", "simHost_0");
                    if (programs.Count >= (int)Invoke(home, "SimHost", "method_0"))
                        return Json(new { ok = false, error = "The home host has no room for another initial EXA." });
                    var codeField = ResolveType("SolutionExa").GetField(Name("F", "SolutionExa", "string_1"), Fields);
                    if (codeField == null) throw new MissingFieldException("SolutionExa", "string_1");
                    Invoke(solution, "Solution", "method_2");
                    exa = programs[programs.Count - 1];
                    codeField.SetValue(exa, code);
                    name = (string)Field(exa, "SolutionExa", "string_0");
                }
                else
                {
                    foreach (object item in programs)
                        if (string.Equals((string)Field(item, "SolutionExa", "string_0"), name, StringComparison.Ordinal))
                        {
                            exa = item;
                            break;
                        }
                    if (exa == null) return Json(new { ok = false, error = "EXA not found." });
                    if (method == "DELETE")
                        Invoke(editor, "EditorScreen", "method_36", exa);
                    else
                    {
                        var codeField = exa.GetType().GetField(Name("F", "SolutionExa", "string_1"), Fields);
                        if (codeField == null) throw new MissingFieldException("SolutionExa", "string_1");
                        codeField.SetValue(exa, code);
                    }
                }
                Invoke(editor, "EditorScreen", "method_32");
                Invoke(editor, "EditorScreen", "method_33");
                Invoke(editor, "EditorScreen", "method_25");
                return Json(new { ok = true, name, action = method == "POST" ? "created" : method == "PUT" ? "updated" : "deleted",
                    message = "The editor will compile and refresh on a subsequent frame." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        // Make the initial EXA list match the submitted programs using the
        // editor's creation and deletion paths.
        internal static string SetPrograms(string body)
        {
            try
            {
                EnsureCompatible();
                if (_runState == "running")
                    return Json(new { ok = false, error = "Pause the active run before changing code." });
                var input = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                if (input == null || !input.ContainsKey("programs") || !(input["programs"] is IList))
                    return Json(new { ok = false, error = "Expected JSON with an array of program strings." });
                IList codes = (IList)input["programs"];
                if (codes.Count < 1 || codes.Count > 26 || codes.Cast<object>().Any(c => !(c is string) || ((string)c).Length > 24000))
                    return Json(new { ok = false, error = "Expected 1–26 program strings, each at most 24000 characters." });
                object editor = Editor();
                if (!(bool)Invoke(editor, "EditorScreen", "method_2"))
                    return Json(new { ok = false, error = "Reset the simulation before editing." });
                object solution = Field(editor, "EditorScreen", "solution_0");
                var programs = (IList)Field(solution, "Solution", "list_0");
                object sim = Field(editor, "EditorScreen", "sim_0");
                object team = Invoke(editor, "EditorScreen", "method_24");
                object teamInfo = ((IDictionary)Field(sim, "Sim", "dictionary_0"))[team];
                if (programs.Count < codes.Count)
                {
                    if (teamInfo == null || (bool)Field(sim, "Sim", "bool_4") ||
                        codes.Count > (int)Field(teamInfo, "Sim/GClass391", "int_0") ||
                        codes.Count > (int)Field(sim, "Sim", "int_6"))
                        return Json(new { ok = false, error = "The editor does not allow another initial EXA here." });
                    object home = Field(teamInfo, "Sim/GClass391", "simHost_0");
                    if (codes.Count > (int)Invoke(home, "SimHost", "method_0"))
                        return Json(new { ok = false, error = "The home host has insufficient room for those initial EXAs." });
                }
                var field = ResolveType("SolutionExa").GetField(Name("F", "SolutionExa", "string_1"), Fields);
                if (field == null) throw new MissingFieldException("SolutionExa", "string_1");
                int removed = 0;
                while (programs.Count > codes.Count)
                {
                    Invoke(editor, "EditorScreen", "method_36", programs[programs.Count - 1]);
                    removed++;
                }
                while (programs.Count < codes.Count)
                    Invoke(solution, "Solution", "method_2");
                for (int i = 0; i < codes.Count; i++)
                    field.SetValue(programs[i], ((string)codes[i]).Replace("\r\n", "\n"));
                Invoke(editor, "EditorScreen", "method_32");
                Invoke(editor, "EditorScreen", "method_33");
                Invoke(editor, "EditorScreen", "method_25");
                return Json(new { ok = true, count = codes.Count, removed, message = "Initial EXA programs submitted; game compiles on the next frame." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string Step()
        {
            try
            {
                EnsureCompatible();
                if (_runState == "running")
                    return Json(new { ok = false, error = "Pause the active run before stepping." });
                object editor = Editor();
                object sim = Field(editor, "EditorScreen", "sim_0");
                if ((bool)Invoke(sim, "Sim", "method_47"))
                    return Json(new { ok = false, error = "Current test is already solved; stepping to the next test is not implemented." });
                int cycle = (int)Invoke(sim, "Sim", "method_52");
                if (ReferenceEquals(_pendingEditor, editor) && cycle <= _pendingCycle)
                    return Json(new { ok = false, error = "A step is still pending; read state before requesting another." });
                _pendingEditor = null;
                string name = Name("M", "EditorScreen", "method_18");
                var method = editor.GetType().GetMethods(Methods).Single(m => m.Name == name && m.GetParameters().Length == 2);
                bool running = (bool)Invoke(editor, "EditorScreen", "method_0");
                object budget = Activator.CreateInstance(method.GetParameters()[1].ParameterType, true, running ? 1 : 0);
                method.Invoke(editor, new[] { (object)false, budget });
                if (running) { _pendingEditor = editor; _pendingCycle = cycle; }
                return Json(new { ok = true, message = running
                    ? "Step requested; wait for the cycle to increase before stepping again."
                    : "Simulation armed at cycle 0; call step again to execute the first instruction." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string Reset()
        {
            try
            {
                EnsureCompatible();
                if (_runState == "running")
                    return Json(new { ok = false, error = "Pause the active run before resetting." });
                Invoke(Editor(), "EditorScreen", "method_19");
                _pendingEditor = null;
                return Json(new { ok = true, message = "Reset requested; it completes on a subsequent game frame." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string ContinueEditing()
        {
            try
            {
                EnsureCompatible();
                object screen = GameState.TopScreen();
                string screenType = screen == null ? null : screen.GetType().Name;
                if (screenType != "PuzzleCompletionScreen" && screenType != "BattleCompletionScreen")
                    return Json(new { ok = false, error = "Open a puzzle or battle completion screen first." });
                object editor = Field(screen, screenType, "editorScreen_0");
                if (editor == null || editor.GetType().Name != "EditorScreen")
                    return Json(new { ok = false, error = "Completion screen has no associated editor." });
                if (screenType == "BattleCompletionScreen" && ReferenceEquals(editor, _runEditor))
                    _battleResult = ReadBattleResult(screen);
                // Same reset and screen-pop used by Continue Editing, without its UI animation.
                Invoke(editor, "EditorScreen", "method_19");
                Invoke(GameState.Instance, "GameLogic", "method_15");
                _pendingEditor = null;
                return Json(new { ok = true, message = "Returned to the puzzle editor; reset completes on a subsequent frame." });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        internal static string BattleResults()
        {
            try
            {
                EnsureCompatible();
                object screen = GameState.TopScreen();
                if (screen == null || screen.GetType().Name != "BattleCompletionScreen")
                    return Json(new { ok = false, error = "Open a battle completion screen first." });
                return Json(new { ok = true, battle_result = ReadBattleResult(screen) });
            }
            catch (Exception e) { return Json(new { ok = false, error = e.GetBaseException().Message }); }
        }

        private static object Editor()
        {
            object editor = GameState.TopScreen();
            if (editor == null || editor.GetType().Name != "EditorScreen")
                throw new InvalidOperationException("Open a puzzle editor first.");
            return editor;
        }

        internal static string Read()
        {
            try
            {
                EnsureCompatible();
                object editor = GameState.TopScreen();
                if (editor == null || editor.GetType().Name != "EditorScreen")
                    return Json(new { ok = false, error = "Open a puzzle editor first.", screen = editor?.GetType().Name });
                object sim = Field(editor, "EditorScreen", "sim_0");
                if (sim == null) return Json(new { ok = false, error = "Simulation is not available yet." });
                object solution = Field(editor, "EditorScreen", "solution_0");
                object puzzle = Invoke(solution, "Solution", "method_0");
                FieldInfo puzzleId = puzzle.GetType().GetField("ID", Fields);
                if (puzzleId == null) throw new MissingFieldException("Puzzle", "ID");

                var exas = new List<object>();
                var files = new List<object>();
                bool showGoal = (bool)Invoke(editor, "EditorScreen", "method_7");
                foreach (object entity in (IEnumerable)Field(sim, "Sim", "list_1"))
                {
                    if (entity.GetType().Name != "SimFile" && entity.GetType().Name != "SimExa") continue;
                    object entityHost = Invoke(entity, "SimEntity", "method_0");
                    if (entityHost != null && (bool)Invoke(entityHost, "SimHost", "method_10", showGoal)) continue;
                    if (entity.GetType().Name == "SimFile")
                    {
                        var values = new List<object>();
                        foreach (object value in (IEnumerable)Field(entity, "SimFile", "list_0"))
                            values.Add(Value(value));
                        object id = Field(entity, "SimFile", "gstruct11_0");
                        object host = Invoke(entity, "SimEntity", "method_0");
                        files.Add(new { id = Invoke(id, "GStruct11", "method_2"),
                            host = host == null ? null : Field(host, "SimHost", "string_0"),
                            values });
                    }
                    else if (entity.GetType().Name == "SimExa")
                    {
                        object host = Invoke(entity, "SimEntity", "method_0");
                        bool error = (bool)Field(entity, "SimExa", "bool_0");
                        exas.Add(new
                        {
                            name = Field(entity, "SimExa", "string_0"),
                            host = host == null ? null : Field(host, "SimHost", "string_0"),
                            instruction_index = Field(entity, "SimExa", "int_0"),
                            x = Value(Field(entity, "SimExa", "exaValue_0")),
                            t = Value(Field(entity, "SimExa", "exaValue_1")),
                            error,
                            error_message = error ? Field(entity, "SimExa", "string_1") : null
                        });
                    }
                }
                var hosts = new List<object>();
                var visibleHosts = new HashSet<object>();
                foreach (object host in (IEnumerable)Field(sim, "Sim", "list_0"))
                {
                    if ((bool)Invoke(host, "SimHost", "method_10", showGoal)) continue;
                    visibleHosts.Add(host);
                    hosts.Add(new { name = Field(host, "SimHost", "string_0") });
                }
                object team = Invoke(editor, "EditorScreen", "method_24");
                var links = new List<object>();
                var hardware = new List<object>();
                foreach (object host in visibleHosts)
                {
                    foreach (object link in (IEnumerable)Field(host, "SimHost", "list_1"))
                    {
                        object from = Field(link, "SimHostLink", "simHost_0");
                        object to = Field(link, "SimHostLink", "simHost_1");
                        if (!ReferenceEquals(host, from) || !visibleHosts.Contains(to)) continue;
                        object fromIds = Field(link, "SimHostLink", "gstruct28_0");
                        object toIds = Field(link, "SimHostLink", "gstruct28_1");
                        links.Add(new {
                            from = Field(from, "SimHost", "string_0"),
                            from_id = Optional(Invoke(fromIds, "GStruct28", "method_2", team)),
                            to = Field(to, "SimHost", "string_0"),
                            to_id = Optional(Invoke(toIds, "GStruct28", "method_2", team)),
                            blocked = Field(link, "SimHostLink", "bool_0")
                        });
                    }
                    foreach (object register in (IEnumerable)Field(host, "SimHost", "list_2"))
                        hardware.Add(new {
                            host = Field(host, "SimHost", "string_0"),
                            name = Field(register, "GClass54", "string_0"),
                            label = Optional(Field(register, "GClass54", "maybe_0")),
                            display_type = Convert.ToInt32(Field(register, "GClass54", "genum14_0"))
                        });
                }
                var goals = new List<string>();
                foreach (object goal in (IEnumerable)Field(sim, "Sim", "list_2"))
                    goals.Add(Convert.ToString(Invoke(goal, "GInterface7", "imethod_0")));
                object info = PuzzleInfo(puzzle);
                string description = Convert.ToString(Field(info, "GClass202", "locString_2"));
                int cycle = (int)Invoke(sim, "Sim", "method_52");
                bool pending = ReferenceEquals(_pendingEditor, editor) && cycle <= _pendingCycle;
                if (!pending) _pendingEditor = null;
                return Json(new
                {
                    ok = true,
                    puzzle_id = puzzleId.GetValue(puzzle),
                    solution_name = Field(solution, "Solution", "string_0"),
                    size_limit = Field(info, "GClass202", "int_2"),
                    cycle,
                    step_pending = pending,
                    solved_current_test = Invoke(sim, "Sim", "method_47"),
                    description,
                    goals,
                    hosts,
                    links,
                    hardware,
                    exas,
                    files,
                    limitations = new[] { "Hardware display_type does not indicate verified access permissions.",
                        "Snapshot is incomplete; hidden hosts and their links are omitted." }
                });
            }
            catch (Exception e)
            {
                return Json(new { ok = false, error = e.GetBaseException().Message });
            }
        }

        private static string Json(object value) => new JavaScriptSerializer().Serialize(value);

        private static object PuzzleInfo(object puzzle)
        {
            Type puzzles = ResolveType("Puzzles");
            var method = puzzles.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(m => m.Name == Name("M", "Puzzles", "smethod_2") && m.GetParameters().Length == 1);
            return method.Invoke(null, new[] { puzzle });
        }

        private static object Optional(object value)
        {
            Type type = value.GetType();
            var present = type.GetMethod(Name("M", "Maybe`1", "method_0"), Methods);
            var get = type.GetMethod(Name("M", "Maybe`1", "method_2"), Methods);
            if (present == null || get == null) throw new InvalidOperationException("Missing optional value methods.");
            return (bool)present.Invoke(value, null) ? get.Invoke(value, null) : null;
        }

        internal static void EnsureCompatible()
        {
            if (_verified) return;
            string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(exe))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (hash != ExpectedHash) throw new InvalidOperationException("Unsupported EXAPUNKS executable SHA-256.");
            }
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "namemap.tsv");
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(path))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 4) map[parts[0] + "\t" + parts[1] + "\t" + parts[2]] = parts[3];
                else if (parts.Length == 3 && parts[0] == "T") map["T\t" + parts[1]] = parts[2];
            }
            _map = map;
            _verified = true;
        }

        internal static string Name(string kind, string type, string member)
        {
            string name;
            if (!_map.TryGetValue(kind + "\t" + type + "\t" + member, out name))
                throw new InvalidOperationException("Missing mapping: " + type + "." + member);
            return name;
        }

        private static object Field(object obj, string type, string name)
        {
            var field = obj.GetType().GetField(Name("F", type, name), Fields);
            if (field == null) throw new MissingFieldException(type, name);
            return field.GetValue(obj);
        }

        private static object Invoke(object obj, string type, string name, params object[] args)
        {
            string realName = Name("M", type, name);
            Type declaringType = ResolveType(type);
            if (declaringType == null || !declaringType.IsInstanceOfType(obj))
                throw new InvalidOperationException("Unexpected declaring type: " + type);
            var matches = declaringType.GetMethods(Methods)
                .Where(m => m.Name == realName && m.GetParameters().Length == args.Length).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Ambiguous or missing method: " + type + "." + name);
            return matches[0].Invoke(obj, args);
        }

        internal static Type ResolveType(string type)
        {
            string runtimeType;
            if (!_map.TryGetValue("T\t" + type, out runtimeType)) runtimeType = type;
            Type result = GameState.GameAssembly.GetType(runtimeType);
            if (result == null) throw new InvalidOperationException("Missing runtime type: " + type);
            return result;
        }

        private static object Value(object value)
        {
            if (value == null) return null;
            return Invoke(value, "ExaValue", "method_2", true);
        }
    }
}
#endif
