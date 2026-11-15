using UnityEngine;
using Zap.Core;

namespace Zap.Constellation
{
    public struct TutorialFacts
    {
        public int Topics;
        public int Links;
        public bool Holding;
        public bool Pulling;
        public bool InWorld;
    }

    public sealed class Onboarding : MonoBehaviour
    {
        public const int StepCount = 8;

        private const string TutorialDoneKey = "zap.tutorialDone";
        private const float RestartHoldSeconds = 1.5f;

        private static readonly string[] Titles =
        {
            "Tutorial 1 of 8 · Make a topic",
            "Tutorial 2 of 8 · Grab it",
            "Tutorial 3 of 8 · Add another",
            "Tutorial 4 of 8 · Link them",
            "Tutorial 5 of 8 · Cut the link",
            "Tutorial 6 of 8 · Move everything",
            "Tutorial 7 of 8 · Step inside",
            "Tutorial 8 of 8 · Look around"
        };

        private static readonly string[] Instructions =
        {
            "Hold A on the right controller, say a topic, then let go.",
            "Reach the topic, squeeze a grip to grab it, then throw it.",
            "Hold A and say a second topic.",
            "Point at one topic, hold the trigger, drag to the other, then let go.",
            "Hold the trigger in empty space and sweep across the line.",
            "Squeeze both grips in empty space and pull. Move your hands apart to stretch.",
            "Point at a topic, then press X on the left controller.",
            "Move and look with the sticks. Press Y to come back."
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
            new[] { new ControlHint(ControlId.RightTrigger, "Hold and drag", true) },
            new[] { new ControlHint(ControlId.RightTrigger, "Hold and sweep", true) },
            new[]
            {
                new ControlHint(ControlId.LeftGrip, "Squeeze", true),
                new ControlHint(ControlId.RightGrip, "Squeeze", true)
            },
            new[] { new ControlHint(ControlId.LeftX, "Enter the topic", true) },
            new[]
            {
                new ControlHint(ControlId.LeftStick, "Move", true),
                new ControlHint(ControlId.RightStick, "Look", true),
                new ControlHint(ControlId.LeftY, "Return", true)
            }
        };

        private static readonly ControlHint[] VoidHints =
        {
            new ControlHint(ControlId.RightA, "Hold: new topic"),
            new ControlHint(ControlId.RightB, "Hold: change topic"),
            new ControlHint(ControlId.RightGrip, "Grab"),
            new ControlHint(ControlId.RightTrigger, "Link or cut"),
            new ControlHint(ControlId.LeftGrip, "Grab"),
            new ControlHint(ControlId.LeftX, "Enter topic"),
            new ControlHint(ControlId.LeftStick, "Hold: tutorial")
        };

        private static readonly ControlHint[] WorldHints =
        {
            new ControlHint(ControlId.LeftStick, "Move"),
            new ControlHint(ControlId.RightStick, "Look"),
            new ControlHint(ControlId.RightA, "Hold: speak"),
            new ControlHint(ControlId.RightB, "Hold: change topic"),
            new ControlHint(ControlId.RightTrigger, "Event"),
            new ControlHint(ControlId.LeftY, "Return")
        };

        private readonly ControllerActions _input = new ControllerActions();
        private ConstellationController _controller;
        private ControllerHints _hints;
        private TutorialFacts _start;
        private bool _sawMiddle;
        private bool _tutorialHeldLastFrame;
        private bool _restartArmed = true;
        private float _restartHeldFor;

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
            if (PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0)
                onboarding.BeginTutorial();
            return onboarding;
        }

        public void Skip()
        {
            Running = false;
            _restartArmed = false;
            _restartHeldFor = 0f;
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            _controller.SetStatus("Tutorial skipped. Hold the left stick to see it again.");
        }

        public void Restart()
        {
            BeginTutorial();
            PlayerPrefs.SetInt(TutorialDoneKey, 0);
        }

        public static bool Middle(int step, TutorialFacts start, TutorialFacts now)
        {
            switch (step)
            {
                case 1: return now.Holding;
                case 5: return now.Pulling;
                case 7: return now.InWorld;
                default: return false;
            }
        }

        public static bool Done(int step, TutorialFacts start, TutorialFacts now, bool sawMiddle)
        {
            switch (step)
            {
                case 0: return now.Topics > start.Topics;
                case 1: return sawMiddle && !now.Holding;
                case 2: return now.Topics > start.Topics;
                case 3: return now.Links > start.Links;
                case 4: return now.Links < start.Links;
                case 5: return sawMiddle && !now.Pulling;
                case 6: return now.InWorld;
                case 7: return sawMiddle && !now.InWorld;
                default: return false;
            }
        }

        private void Update()
        {
            if (_controller == null || _hints == null) return;

            _input.Poll();
            bool tutorialHeld = _input.TutorialHeld;
            bool freshPress = tutorialHeld && !_tutorialHeldLastFrame;
            if (!tutorialHeld)
            {
                _restartArmed = true;
                _restartHeldFor = 0f;
            }

            if (Running)
            {
                if (freshPress)
                    Skip();
                else
                    UpdateTutorial();
            }
            else if (!_controller.InWorld && tutorialHeld && _restartArmed)
            {
                _restartHeldFor += Time.unscaledDeltaTime;
                if (_restartHeldFor >= RestartHoldSeconds)
                {
                    _restartArmed = false;
                    _restartHeldFor = 0f;
                    Restart();
                }
            }
            else if (_controller.InWorld)
            {
                _restartHeldFor = 0f;
            }

            if (Running)
                _hints.Show(StepHints[Step], true);
            else
                _hints.Show(_controller.InWorld ? WorldHints : VoidHints, false);

            _tutorialHeldLastFrame = tutorialHeld;
        }

        private void BeginTutorial()
        {
            Step = 0;
            Running = true;
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
            _restartArmed = false;
            _restartHeldFor = 0f;
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            _controller.SetStatus("You're set. Hold the left stick to see the tutorial again.");
        }

        private TutorialFacts ReadFacts()
        {
            return new TutorialFacts
            {
                Topics = _controller.Balls.Count,
                Links = _controller.LinkCount,
                Holding = _controller.IsHolding,
                Pulling = _controller.IsPulling,
                InWorld = _controller.InWorld
            };
        }
    }
}
