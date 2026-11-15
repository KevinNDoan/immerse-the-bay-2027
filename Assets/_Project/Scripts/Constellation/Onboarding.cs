using UnityEngine;
using Zap.Core;

namespace Zap.Constellation
{
    public struct TutorialFacts
    {
        public int Topics;
        public int Links;
        // Running totals that only ever go up, so deleting a topic cannot undo progress.
        public int Created;
        public int LinkChanges;
        public bool Holding;
        public bool Pulling;
        public bool InWorld;
    }

    public sealed class Onboarding : MonoBehaviour
    {
        public const int StepCount = 4;

        private const string TutorialDoneKey = "zap.tutorialDone";
        private const float SkipHoldSeconds = 0.7f;
        private const float RestartHoldSeconds = 1.5f;

        private static readonly string[] Titles =
        {
            "1 of 4 · Say a topic",
            "2 of 4 · Move it",
            "3 of 4 · Connect ideas",
            "4 of 4 · Step inside"
        };

        private static readonly string[] Instructions =
        {
            "Hold A and say a topic, like 'volcanoes'. Let go when you're done.",
            "Squeeze the grip to pick it up. Let go gently to place it. Throw forward to enter its world.",
            "Say another topic. To link or unlink two, tap them together.",
            "Aim at a topic and press X to step inside. Press Y to come back."
        };

        private static readonly ControlHint[][] StepHints =
        {
            new[] { new ControlHint(ControlId.RightA, "Hold and speak", true) },
            new[]
            {
                new ControlHint(ControlId.RightGrip, "Grab", true),
                new ControlHint(ControlId.LeftGrip, "Grab", true)
            },
            new[] { new ControlHint(ControlId.RightA, "Hold and speak", true) },
            new[]
            {
                new ControlHint(ControlId.LeftX, "Step inside", true),
                new ControlHint(ControlId.LeftY, "Come back", true)
            }
        };

        private static readonly ControlHint[] VoidHints =
        {
            new ControlHint(ControlId.RightA, "Hold: new topic"),
            new ControlHint(ControlId.RightB, "Hold: change topic"),
            new ControlHint(ControlId.RightGrip, "Grab · throw to travel"),
            new ControlHint(ControlId.LeftGrip, "Grab · throw to travel"),
            new ControlHint(ControlId.RightTrigger, "Aim and drag: link"),
            new ControlHint(ControlId.LeftX, "Step inside"),
            new ControlHint(ControlId.LeftStick, "Hold: tutorial")
        };

        private static readonly ControlHint[] WorldHints =
        {
            new ControlHint(ControlId.LeftStick, "Walk"),
            new ControlHint(ControlId.RightStick, "Look / zoom object"),
            new ControlHint(ControlId.RightA, "Hold: new idea"),
            new ControlHint(ControlId.RightB, "Hold: change topic"),
            new ControlHint(ControlId.RightTrigger, "Explore object"),
            new ControlHint(ControlId.LeftTrigger, "Hold: ask / create object"),
            new ControlHint(ControlId.RightGrip, "Move object / pane"),
            new ControlHint(ControlId.LeftGrip, "Move object / pane"),
            new ControlHint(ControlId.LeftX, "Linked world / recenter"),
            new ControlHint(ControlId.LeftY, "Return to map")
        };

        private static readonly ControlHint[] TravelHints = { new ControlHint(ControlId.LeftY, "Return to map") };
        private static readonly ControlHint[] CancelHints = { new ControlHint(ControlId.LeftY, "Cancel request", true) };

        private readonly ControllerActions _input = new ControllerActions();
        private ConstellationController _controller;
        private ControllerHints _hints;
        private TutorialFacts _start;
        private bool _sawMiddle;
        private bool _holdArmed;
        private float _heldFor;

        public bool Running { get; private set; }
        public int Step { get; private set; }
        public string Title => Running ? Titles[Step] : null;
        public string Text => Running ? Instructions[Step] : null;

        public static Onboarding Create(ConstellationController controller, ControllerHints hints)
        {
            var root = new GameObject("ZAP onboarding");
            root.transform.SetParent(controller.transform, false);
            var onboarding = root.AddComponent<Onboarding>();
            onboarding._controller = controller;
            onboarding._hints = hints;
            if (controller.Balls.Count == 0 && PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0)
                onboarding.BeginTutorial();
            return onboarding;
        }

        public void Skip()
        {
            Running = false;
            _holdArmed = false;
            _heldFor = 0f;
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
            _controller.SetStatus("Tutorial skipped. Hold the left stick to see it again.");
        }

        public void Restart()
        {
            BeginTutorial();
            PlayerPrefs.SetInt(TutorialDoneKey, 0);
            PlayerPrefs.Save();
        }

        public static bool Middle(int step, TutorialFacts start, TutorialFacts now)
        {
            switch (step)
            {
                case 1: return now.Holding;
                default: return false;
            }
        }

        public static bool Done(int step, TutorialFacts start, TutorialFacts now, bool sawMiddle)
        {
            switch (step)
            {
                case 0: return now.Created > start.Created;
                case 1: return sawMiddle && !now.Holding;
                // Either half of the step counts: a new topic, or linking or unlinking two.
                case 2: return now.Created > start.Created || now.LinkChanges > start.LinkChanges;
                case 3: return now.InWorld;
                default: return false;
            }
        }

        private void Update()
        {
            if (_controller == null || _hints == null) return;

            _input.Poll();
            bool tutorialHeld = _input.TutorialHeld;
            if (!tutorialHeld)
            {
                _holdArmed = true;
                _heldFor = 0f;
            }
            else if (!_holdArmed || _controller.WorldActive)
            {
                _heldFor = 0f;
            }
            else if (Running)
            {
                _heldFor += Time.unscaledDeltaTime;
                if (_heldFor >= SkipHoldSeconds)
                {
                    _holdArmed = false;
                    _heldFor = 0f;
                    Skip();
                }
            }
            else
            {
                _heldFor += Time.unscaledDeltaTime;
                if (_heldFor >= RestartHoldSeconds)
                {
                    _holdArmed = false;
                    _heldFor = 0f;
                    Restart();
                }
            }

            if (Running)
                UpdateTutorial();

            if (_controller.CanCancelVoice)
                _hints.Show(CancelHints, false);
            else if (_controller.IsTravelling)
                _hints.Show(TravelHints, false);
            else if (_controller.WorldActive)
                _hints.Show(WorldHints, false);
            else if (Running)
                _hints.Show(StepHints[Step], true);
            else
                _hints.Show(VoidHints, false);
        }

        private void BeginTutorial()
        {
            Step = 0;
            Running = true;
            // The idle status repeats step 1's instruction, so start with the status line empty.
            if (_controller != null) _controller.SetStatus(string.Empty);
            BeginStep();
        }

        private void BeginStep()
        {
            _start = ReadFacts();
            _sawMiddle = false;
        }

        private void UpdateTutorial()
        {
            TutorialFacts now = ReadFacts();
            if (Middle(Step, _start, now))
                _sawMiddle = true;
            if (!Done(Step, _start, now, _sawMiddle)) return;

            Step++;
            if (Step < StepCount)
            {
                BeginStep();
                return;
            }

            Step = StepCount - 1;
            Running = false;
            _holdArmed = false;
            _heldFor = 0f;
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
            _controller.SetStatus("That's it! Look at a controller to see its buttons.");
        }

        private TutorialFacts ReadFacts()
        {
            return new TutorialFacts
            {
                Topics = _controller.Balls.Count,
                Links = _controller.LinkCount,
                Created = _controller.TopicsCreated,
                LinkChanges = _controller.LinkChanges,
                Holding = _controller.IsHolding,
                Pulling = _controller.IsPulling,
                InWorld = _controller.InWorld
            };
        }
    }
}
