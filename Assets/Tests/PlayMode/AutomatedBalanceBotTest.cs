using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BalancePuzzle.Tests
{
    /// <summary>
    /// Autonomous balance playtester for Stage 1 (Stage1_Sandbox).
    ///
    /// Drives the REAL gameplay stack — no placeholder movement, no 2D logic:
    ///   Raycast selection (StoneSelector) -> kinematic XZ drag with MovePosition
    ///   (StoneDragger) -> Y-rotation (StoneRotator) -> release -> 2.0 s calm
    ///   evaluation (StabilityEvaluator) -> step lock + time bonus
    ///   (StepLockManager + LevelTimer) -> reset bus (LevelReset / StoneState).
    ///
    /// Input injection strategy: InputReader is the sole Input System consumer, so
    /// the bot raises its public events (SelectPerformed / PointerMoved /
    /// SelectReleased) through their backing delegates and projects world targets
    /// to screen space. Event dispatch is synchronous, so selection and grab state
    /// assert immediately; pointer streaming yields per FixedUpdate because
    /// StoneDragger consumes positions in FixedUpdate via MovePosition. The real
    /// StoneSelector raycast, the real StoneDragger grab/move/release path
    /// (kinematic + MovePosition + WakeUp + LevelController.BeginEvaluation) and
    /// the real StoneRotator polling (fed via the public SetRotateAxis) all run
    /// unmodified. If event injection cannot engage (e.g. headless camera
    /// framing), the bot falls back to a physics-level mirror that performs the
    /// exact same Rigidbody operation order as StoneDragger.HandleSelect /
    /// HandleRelease, so the stability/lock assertions still exercise production
    /// code (StabilityEvaluator -> StepLockManager -> LevelTimer).
    ///
    /// Telemetry: Application.logMessageReceived is intercepted in SetUp; every
    /// TearDown appends this test's outcome to the cumulative report at
    /// <project-root>/AI_Test_Report.json (timestamp, status, totalTests,
    /// totalErrors, errors with message + stackTrace).
    ///
    /// Allocation discipline: no LINQ, no closures and no per-frame allocations
    /// in the hot loops below (cached yield instructions, pre-sized lists,
    /// named methods instead of lambdas).
    /// </summary>
    public sealed class AutomatedBalanceBotTest
    {
        private const string SceneName = "Stage1_Sandbox";
        private const string ReportFileName = "AI_Test_Report.json";
        private const float PositionTolerance = 0.05f;
        private const float TimerTolerance = 1.0f;
        private const float SettleVelocityTolerance = 0.3f;

        private static readonly List<TestRecord> s_records = new List<TestRecord>(8);
        private static readonly List<ErrorRecord> s_allErrors = new List<ErrorRecord>(16);

        private readonly List<ErrorRecord> _testErrors = new List<ErrorRecord>(8);
        private readonly WaitForFixedUpdate _fixedUpdate = new WaitForFixedUpdate();

        private float _testStartRealtime;
        private bool _timeChangedFired;

        // ---- scene handles (resolved per test) ----
        private LevelController _controller;
        private LevelTimer _timer;
        private StepLockManager _locks;
        private StabilityEvaluator _evaluator;
        private LevelReset _reset;
        private StoneSelector _selector;
        private StoneDragger _dragger;
        private InputReader _input;
        private Stage1Config _config;
        private GameObject _platform;
        private GameObject _stoneA;
        private GameObject _stoneB;

        // ============================ lifecycle ============================

        [SetUp]
        public void SetUp()
        {
            _testErrors.Clear();
            _testStartRealtime = Time.realtimeSinceStartup;
            _timeChangedFired = false;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLogMessageReceived;

            string testName = TestContext.CurrentContext.Test.Name;
            bool outcomePassed = TestContext.CurrentContext.Result.Outcome.Status
                == NUnit.Framework.Interfaces.TestStatus.Passed;
            bool passed = outcomePassed && _testErrors.Count == 0;

            s_records.Add(new TestRecord
            {
                testName = testName,
                status = passed ? "PASSED" : "FAILED",
                durationSeconds = Time.realtimeSinceStartup - _testStartRealtime,
                errors = _testErrors.ToArray(),
            });
            for (int i = 0; i < _testErrors.Count; i++)
                s_allErrors.Add(_testErrors[i]);

            WriteReport();
        }

        private void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
                return;
            _testErrors.Add(new ErrorRecord
            {
                test = TestContext.CurrentContext.Test.Name,
                type = type.ToString(),
                message = message ?? string.Empty,
                stackTrace = stackTrace ?? string.Empty,
            });
        }

        // ============================ test 1: full stack ============================

        [UnityTest, Order(1)]
        public IEnumerator BotStacksStoneA_LocksAndAppliesBonus()
        {
            yield return LoadSandboxAndResolve();

            Assert.IsNotNull(_stoneA, "Stone_A must exist (see Stage1HealthCheck).");
            Assert.IsNotNull(_platform, "Platform (altar) must exist.");
            Assert.AreEqual(LevelState.Placing, _controller.State, "Fresh scene must start in Placing.");

            var bodyA = _stoneA.GetComponent<Rigidbody>();
            Assert.IsNotNull(bodyA, "Stone_A needs a Rigidbody.");
            Assert.IsFalse(bodyA.isKinematic, "Stone_A must start dynamic.");

            // --- altar target from live scene geometry (never hard-coded) ---
            var platformCollider = _platform.GetComponent<Collider>();
            Assert.IsNotNull(platformCollider, "Platform needs a Collider.");
            float topY = platformCollider.bounds.max.y;
            float halfHeightA = _config.stones[0].size.y * 0.5f;
            Vector3 altarTop = new Vector3(
                _platform.transform.position.x,
                topY + halfHeightA + 0.02f,
                _platform.transform.position.z);

            float timeBefore = _timer.TimeLeft;
            float wallBefore = Time.realtimeSinceStartup;
            float bonusStep0 = _config.stepTimeBonuses[0];
            _timer.TimeChanged += OnTimeChangedFlag;

            // --- simulate the player: select -> drag over altar -> rotate -> release ---
            if (!TryEngageInputDrag(_stoneA))
            {
                _timer.TimeChanged -= OnTimeChangedFlag;
                yield return MirrorDraggerPhysics(_stoneA, altarTop);
                _timer.TimeChanged += OnTimeChangedFlag;
            }
            else
            {
                yield return StreamDragToTarget(_stoneA, altarTop);

                // Rotation pass through the REAL StoneRotator (polls RotateAxis
                // while StoneDragger.IsDragging; Stone_A has zero COM offset so
                // yaw cannot break the support check below).
                Quaternion before = _stoneA.transform.rotation;
                _input.SetRotateAxis(1f);
                float rotateEnd = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < rotateEnd)
                    yield return null;
                _input.SetRotateAxis(0f);
                Assert.IsFalse(before == _stoneA.transform.rotation,
                    "StoneRotator must Y-rotate the held stone while dragging.");

                ReleaseViaInput();
                Assert.AreEqual(LevelState.Evaluating, _controller.State,
                    "Release must hand the stone to LevelController.BeginEvaluation.");

                for (int i = 0; i < 5; i++)
                    yield return _fixedUpdate;
            }

            // --- wait out the calm window (stabilityDuration + margin) ---
            float deadline = Time.realtimeSinceStartup + _config.stabilityDuration + 8f;
            while ((_evaluator.IsEvaluating || _controller.State == LevelState.Evaluating)
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            _timer.TimeChanged -= OnTimeChangedFlag;

            if (_controller.State == LevelState.Failed)
                Assert.Fail("Evaluation failed unexpectedly: reason='" + _controller.FailReason
                    + "' evaluator detail='" + _evaluator.LastDetail + "'.");

            Assert.IsFalse(_evaluator.IsEvaluating, "Evaluator must reach a terminal result.");
            Assert.IsTrue(_locks.IsLocked(_stoneA), "Stone_A must be locked after a calm placement.");
            Assert.IsTrue(bodyA.isKinematic, "Locked stone must be kinematic (StepLockManager.TryLockCandidate).");
            Assert.AreEqual(1, _locks.LockedCount, "Exactly one step must be locked.");
            Assert.AreEqual(LevelState.Placing, _controller.State, "Non-final lock returns to Placing.");

            // Bonus assertion, compensated for wall-clock countdown elapsed since timeBefore.
            float elapsed = Time.realtimeSinceStartup - wallBefore;
            float expected = Mathf.Min(timeBefore + bonusStep0, _config.maxTime) - elapsed;
            Assert.IsTrue(_timeChangedFired, "LevelTimer must emit TimeChanged (HUD contract).");
            Assert.AreEqual(expected, _timer.TimeLeft, TimerTolerance,
                "Step bonus " + bonusStep0 + "s must be applied (capped at maxTime).");
            Assert.LessOrEqual(_timer.TimeLeft, _config.maxTime, "Timer must never exceed maxTime.");
        }

        // ============================ test 2: reset ============================

        [UnityTest, Order(2)]
        public IEnumerator ResetRestoresStoneSnapshots()
        {
            yield return LoadSandboxAndResolve();

            GameObject[] stones = FindAllStones();
            for (int i = 0; i < stones.Length; i++)
                Assert.IsNotNull(stones[i], "Stone index " + i + " must exist in the scene.");

            Vector3[] snapshot = new Vector3[stones.Length];
            for (int i = 0; i < stones.Length; i++)
                snapshot[i] = stones[i].transform.position;

            // Disturb the world: displace Stone_A, then reset via the real bus.
            var bodyA = _stoneA.GetComponent<Rigidbody>();
            bodyA.position = snapshot[0] + new Vector3(3f, 2f, 3f);
            yield return _fixedUpdate;
            yield return _fixedUpdate;

            float timeBeforeReset = _timer.TimeLeft;
            Assert.Less(timeBeforeReset, _config.initialTime + TimerTolerance,
                "Sanity: countdown must be running before reset.");

            _reset.RequestReset();
            yield return _fixedUpdate;
            yield return _fixedUpdate;
            yield return null;

            for (int i = 0; i < stones.Length; i++)
            {
                Vector3 p = stones[i].transform.position;
                Assert.AreEqual(snapshot[i].x, p.x, PositionTolerance,
                    stones[i].name + " X must return to its StoneState snapshot.");
                Assert.AreEqual(snapshot[i].y, p.y, PositionTolerance,
                    stones[i].name + " Y must return to its StoneState snapshot.");
                Assert.AreEqual(snapshot[i].z, p.z, PositionTolerance,
                    stones[i].name + " Z must return to its StoneState snapshot.");
                var body = stones[i].GetComponent<Rigidbody>();
                Assert.Less(body.linearVelocity.magnitude, SettleVelocityTolerance,
                    stones[i].name + " velocity must be cleared by StoneState.ResetState.");
                Assert.Less(body.angularVelocity.magnitude, SettleVelocityTolerance,
                    stones[i].name + " angular velocity must be cleared by StoneState.ResetState.");
                Assert.IsFalse(body.isKinematic,
                    stones[i].name + " must be dynamic again after reset.");
            }
            Assert.AreEqual(0, _locks.LockedCount, "Reset must clear the lock registry.");
            Assert.AreEqual(LevelState.Placing, _controller.State, "Reset must return to Placing.");
            Assert.IsFalse(_evaluator.IsEvaluating, "Reset must stop any evaluation.");
            Assert.AreEqual(_config.initialTime, _timer.TimeLeft, TimerTolerance,
                "Reset must restore the initial countdown.");
        }

        // ============================ test 3: fail state ============================

        [UnityTest, Order(3)]
        public IEnumerator FellStoneFailsCleanlyWithoutNullReference()
        {
            yield return LoadSandboxAndResolve();

            // Stone_B below killY, then evaluate: must Fail with a reason, never NRE.
            var bodyB = _stoneB.GetComponent<Rigidbody>();
            bodyB.linearVelocity = Vector3.zero;
            bodyB.angularVelocity = Vector3.zero;
            bodyB.position = new Vector3(0f, _config.killY - 1f, 0f);
            yield return _fixedUpdate;

            _controller.BeginEvaluation(_stoneB);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_controller.State != LevelState.Failed && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(LevelState.Failed, _controller.State, "A stone below killY must fail the evaluation.");
            Assert.IsFalse(string.IsNullOrEmpty(_controller.FailReason), "Fail must carry a reason.");
            Assert.IsFalse(_locks.IsLocked(_stoneB), "A failed stone must never lock.");

            for (int i = 0; i < _testErrors.Count; i++)
                Assert.IsFalse(_testErrors[i].message.Contains("NullReferenceException"),
                    "Fail-state must not throw NullReferenceException: " + _testErrors[i].message);
        }

        // ============================ test 4: timer expiry ============================

        [UnityTest, Order(4)]
        public IEnumerator TimerExpiryFailsCleanly()
        {
            yield return LoadSandboxAndResolve();
            Assert.AreEqual(LevelState.Placing, _controller.State);

            // Drain the real timer through its public API (AddBonus clamps only at
            // the top, so a large negative bonus drives it to expiry on Update).
            _timer.AddBonus(-(_config.initialTime + 60f));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_controller.State != LevelState.Failed && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(LevelState.Failed, _controller.State, "Timer expiry must fail the level.");
            Assert.AreEqual("Time up.", _controller.FailReason, "Timer expiry reason mismatch.");
            Assert.AreEqual(0f, _timer.TimeLeft, "Expired timer must read exactly zero.");

            for (int i = 0; i < _testErrors.Count; i++)
                Assert.IsFalse(_testErrors[i].message.Contains("NullReferenceException"),
                    "Timer expiry must not throw NullReferenceException: " + _testErrors[i].message);
        }

        // ============================ drag helpers ============================

        private void OnTimeChangedFlag(float _)
        {
            _timeChangedFired = true;
        }

        private IEnumerator LoadSandboxAndResolve()
        {
            // Always reload: PlayMode tests share one session, so a previous test
            // would otherwise leak locked stones / Failed state into this one.
            // (Regression: conditional load caused ResetRestoresStoneSnapshots and
            // TimerExpiryFailsCleanly to observe test 1 / test 3 leftovers.)
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return null;

            _controller = UnityEngine.Object.FindFirstObjectByType<LevelController>();
            _timer = UnityEngine.Object.FindFirstObjectByType<LevelTimer>();
            _locks = UnityEngine.Object.FindFirstObjectByType<StepLockManager>();
            _evaluator = UnityEngine.Object.FindFirstObjectByType<StabilityEvaluator>();
            _reset = UnityEngine.Object.FindFirstObjectByType<LevelReset>();
            _selector = UnityEngine.Object.FindFirstObjectByType<StoneSelector>();
            _dragger = UnityEngine.Object.FindFirstObjectByType<StoneDragger>();
            _input = UnityEngine.Object.FindFirstObjectByType<InputReader>();

            Assert.IsNotNull(_controller, "LevelController missing in " + SceneName + ".");
            Assert.IsNotNull(_timer, "LevelTimer missing in " + SceneName + ".");
            Assert.IsNotNull(_locks, "StepLockManager missing in " + SceneName + ".");
            Assert.IsNotNull(_evaluator, "StabilityEvaluator missing in " + SceneName + ".");
            Assert.IsNotNull(_reset, "LevelReset missing in " + SceneName + ".");
            Assert.IsNotNull(_selector, "StoneSelector missing in " + SceneName + ".");
            Assert.IsNotNull(_dragger, "StoneDragger missing in " + SceneName + ".");
            Assert.IsNotNull(_input, "InputReader missing in " + SceneName + ".");

            _config = _controller.Config;
            Assert.IsNotNull(_config, "LevelController.Config (Stage1Config) must be wired.");

            _platform = GameObject.Find("Platform");
            _stoneA = GameObject.Find("Stone_A");
            _stoneB = GameObject.Find("Stone_B");

            // Let Start() wiring + first physics steps settle.
            yield return _fixedUpdate;
            yield return _fixedUpdate;
            yield return null;
        }

        private GameObject[] FindAllStones()
        {
            return new[]
            {
                GameObject.Find("Stone_A"),
                GameObject.Find("Stone_B"),
                GameObject.Find("Stone_C"),
                GameObject.Find("Stone_D"),
            };
        }

        /// <summary>
        /// Raises a tap on the stone through InputReader's real event. Dispatch is
        /// synchronous, so the genuine StoneSelector raycast + StoneDragger grab
        /// have already run on return. True when the dragger engaged.
        /// </summary>
        private bool TryEngageInputDrag(GameObject stone)
        {
            Camera cam = Camera.main;
            if (cam == null || _selector.SelectedStone != null)
                return false;

            Vector3 screen = cam.WorldToScreenPoint(stone.transform.position);
            if (screen.z <= 0f)
                return false;

            if (!RaiseSelectPerformed(new Vector2(screen.x, screen.y)))
                return false;

            return _selector.SelectedStone == stone && _dragger.IsDragging;
        }

        /// <summary>
        /// Streams pointer positions toward the world target; the real
        /// StoneDragger.FixedUpdate consumes them with MovePosition.
        /// </summary>
        private IEnumerator StreamDragToTarget(GameObject stone, Vector3 target)
        {
            Camera cam = Camera.main;
            Vector3 start = stone.transform.position;
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 world = Vector3.Lerp(start, target, i / (float)steps);
                Vector3 screen = cam.WorldToScreenPoint(world);
                RaisePointerMoved(new Vector2(screen.x, screen.y));
                yield return _fixedUpdate;
            }
        }

        private void ReleaseViaInput()
        {
            var field = typeof(InputReader).GetField("SelectReleased",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException("InputReader.SelectReleased backing field not found.");
            var del = field.GetValue(_input) as Action;
            if (del == null)
                throw new InvalidOperationException("Nothing is subscribed to InputReader.SelectReleased.");
            del.Invoke();
        }

        private bool RaiseSelectPerformed(Vector2 screenPos)
        {
            var field = typeof(InputReader).GetField("SelectPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return false;
            var del = field.GetValue(_input) as Action<Vector2>;
            if (del == null)
                return false;
            del.Invoke(screenPos);
            return true;
        }

        private bool RaisePointerMoved(Vector2 screenPos)
        {
            var field = typeof(InputReader).GetField("PointerMoved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return false;
            var del = field.GetValue(_input) as Action<Vector2>;
            if (del == null)
                return false;
            del.Invoke(screenPos);
            return true;
        }

        /// <summary>
        /// Deterministic fallback: performs the exact Rigidbody operation order of
        /// StoneDragger.HandleSelect (zero velocities BEFORE kinematic, then
        /// MovePosition per FixedUpdate) and HandleRelease (kinematic off, WakeUp,
        /// LevelController.BeginEvaluation). Used only if input injection cannot
        /// engage (e.g. headless camera framing).
        /// </summary>
        private IEnumerator MirrorDraggerPhysics(GameObject stone, Vector3 target)
        {
            Debug.LogWarning("[BOT] Falling back to physics-mirror drag for '" + stone.name + "'.");
            var body = stone.GetComponent<Rigidbody>();
            Vector3 start = body.position;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                body.MovePosition(Vector3.Lerp(start, target, i / (float)steps));
                yield return _fixedUpdate;
            }

            body.isKinematic = false;
            body.WakeUp();
            _controller.BeginEvaluation(stone);

            // Settle frames so ContactTracker reports live contacts.
            for (int i = 0; i < 5; i++)
                yield return _fixedUpdate;
        }

        // ============================ report ============================

        private static void WriteReport()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string path = Path.Combine(projectRoot, ReportFileName);

                int passed = 0;
                for (int i = 0; i < s_records.Count; i++)
                    if (s_records[i].status == "PASSED")
                        passed++;

                var report = new ReportFile
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    status = (s_records.Count > 0 && passed == s_records.Count && s_allErrors.Count == 0)
                        ? "PASSED" : "FAILED",
                    totalTests = s_records.Count,
                    passed = passed,
                    failed = s_records.Count - passed,
                    totalErrors = s_allErrors.Count,
                    errors = s_allErrors.ToArray(),
                    results = s_records.ToArray(),
                };

                var sb = new StringBuilder(4096);
                sb.Append(JsonUtility.ToJson(report, true));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.Log("AutomatedBalanceBotTest: could not write " + ReportFileName + ": " + ex.Message);
            }
        }

        [Serializable]
        private sealed class ReportFile
        {
            public string timestamp;
            public string status;
            public int totalTests;
            public int passed;
            public int failed;
            public int totalErrors;
            public ErrorRecord[] errors;
            public TestRecord[] results;
        }

        [Serializable]
        private sealed class TestRecord
        {
            public string testName;
            public string status;
            public float durationSeconds;
            public ErrorRecord[] errors;
        }

        [Serializable]
        private sealed class ErrorRecord
        {
            public string test;
            public string type;
            public string message;
            public string stackTrace;
        }
    }
}
