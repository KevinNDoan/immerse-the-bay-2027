using System;
using UnityEngine;

namespace Zap.Constellation
{
    [Serializable] public class StudyModelData
    {
        public string title, explanation, eventPrompt;
        public float displayScale = 1;
        public StudyPartData[] parts;
        public StudyStepData[] steps;
        public StudyExperimentData experiment;
    }
    [Serializable] public class StudyPartData
    {
        public string name, detail, shape, color, motion;
        public Vector3 position, scale, rotation;
        public Vector3[] points;
        public float speed;
        public StudyCurveData curve;
        public StudyRepeatData repeat;
        public StudyLinksData links;
    }
    [Serializable] public class StudyCurveData { public float radius = .25f, height = 1, turns = 2, phase; }
    [Serializable] public class StudyRepeatData { public int count = 1; public float angle; public Vector3 step; }
    [Serializable] public class StudyLinksData { public int from, to, count = 16; public float start, end = 1; }
    [Serializable] public class StudyChangeData
    {
        public int part;
        public Vector3 offset, rotation;
        public Vector3 scale = Vector3.one;
        public bool visible = true;
    }
    [Serializable] public class StudyStepData
    {
        public string title, explanation;
        public int[] focus;
        public StudyChangeData[] changes;
    }
    [Serializable] public class StudyExperimentData
    {
        public string label, lowLabel, highLabel, lowExplanation, highExplanation;
        public StudyChangeData[] changes;
    }
    [Serializable] public class StudyReply
    {
        public string action, answer;
        public StudyModelData model;
    }
    [Serializable] public class PreparedWorld
    {
        public StudyModelData plan;
        public WorldPaneData pane;
        // JsonUtility can materialize a JSON null object as an empty serializable
        // instance. Presence of actual geometry is the boundary, not plan != null.
        public bool HasArtifact => plan?.parts != null && plan.parts.Length > 0;
        public string image, projection, notice, cacheKey, state;
        public Zap.World.EnvironmentManifest environment;
    }
    [Serializable] public class WorldPaneData { public string title, text; }
}
