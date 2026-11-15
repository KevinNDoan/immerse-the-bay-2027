using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zap.Constellation
{
    public sealed partial class StudyWorkspace
    {
        public StudyObjectData[] CaptureObjects()
        {
            var result = new List<StudyObjectData>();
            foreach (var item in _objects)
                if (item.HasModel) result.Add(new StudyObjectData { id = item.ObjectId, model = item.Data });
            return result.ToArray();
        }

        // Validate every target and build every replacement before removing any original.
        private bool ApplyActions(StudyReply reply, StudyExhibit feedback)
        {
            var actions = reply.actions;
            if (actions == null || actions.Length == 0 || actions.Length > 6) return false;
            var targets = new StudyExhibit[actions.Length];
            var touched = new HashSet<string>();
            int count = _objects.FindAll(item => item.HasModel).Count;
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null) return false;
                if (action.action == "add")
                {
                    if (!string.IsNullOrEmpty(action.targetId)) return false;
                    count++;
                }
                else if (action.action == "revise" || action.action == "remove")
                {
                    var target = _objects.Find(item => item.HasModel && item.ObjectId == action.targetId);
                    if (target == null || !touched.Add(target.ObjectId)) return false;
                    targets[i] = target;
                    if (action.action == "remove") count--;
                }
                else return false;
                // JsonUtility can turn a null model on a remove into an empty instance.
                if (action.action == "remove")
                {
                    if (action.model?.parts != null && action.model.parts.Length > 0) return false;
                }
                else if (action.model?.parts == null || action.model.parts.Length == 0 || action.model.parts.Length > 28)
                    return false;
            }
            if (count > 6) return false;

            var staged = new List<StudyExhibit>();
            var replacements = new StudyExhibit[actions.Length];
            StudyExhibit guide = null;
            bool committed = false;
            try
            {
                int additions = 0;
                for (int i = 0; i < actions.Length; i++)
                {
                    if (actions[i].action == "remove") continue;
                    var exhibit = Stage(actions[i].model, staged);
                    if (exhibit.Pane == null || exhibit.Grab == null || !exhibit.HasModel) return false;
                    replacements[i] = exhibit;
                    if (targets[i] != null)
                    {
                        exhibit.ObjectId = targets[i].ObjectId;
                        CopyPlacement(targets[i], exhibit);
                    }
                    else
                    {
                        // Make room for newly added objects without moving existing ones.
                        var right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                        for (int attempt = 0; attempt < 12; attempt++)
                        {
                            bool occupied = _objects.Exists(item => item.HasModel && !touched.Contains(item.ObjectId)
                                && Vector3.Distance(item.Pose.position, exhibit.Pose.position) < .75f);
                            foreach (var other in staged)
                                if (other != exhibit && other.HasModel && Vector3.Distance(other.Pose.position, exhibit.Pose.position) < .75f)
                                    occupied = true;
                            if (!occupied) break;
                            exhibit.Pose.position += right * (additions % 2 == 0 ? .85f : -.85f);
                        }
                        additions++;
                    }
                }
                // Keep a place for answers after deleting the last learning object. Saving
                // this pane prevents Open from restoring the original model on the next visit.
                if (count == 0 && !_objects.Exists(item => !item.HasModel))
                {
                    guide = Stage(new StudyModelData { title = "Explore this world", explanation = reply.answer,
                        parts = Array.Empty<StudyPartData>() }, staged);
                    if (guide.Pane == null || guide.Pose == null) return false;
                    if (feedback != null) CopyPlacement(feedback, guide);
                }

                feedback?.ClearRequest();
                StudyExhibit answerTarget = null;
                for (int i = 0; i < actions.Length; i++)
                {
                    var original = targets[i];
                    var replacement = replacements[i];
                    int index = original != null ? _objects.IndexOf(original) : _objects.Count;
                    if (original != null)
                    {
                        _objects.Remove(original);
                        original.gameObject.SetActive(false);
                        original.Clear(); Destroy(original.gameObject);
                    }
                    if (replacement != null)
                    {
                        _objects.Insert(index, replacement);
                        replacement.gameObject.SetActive(true);
                        answerTarget = replacement;
                    }
                }
                if (guide != null) { _objects.Add(guide); guide.gameObject.SetActive(true); answerTarget = guide; }
                if (answerTarget == null && feedback != null && _objects.Contains(feedback)) answerTarget = feedback;
                if (answerTarget == null) answerTarget = _objects[0];
                SetCurrent(answerTarget); answerTarget.Explain(reply.answer);
                committed = true;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Object changes could not be prepared: " + error.GetType().Name);
                return false;
            }
            finally
            {
                if (!committed)
                    foreach (var item in staged) { item.gameObject.SetActive(false); item.Clear(); Destroy(item.gameObject); }
            }
        }

        private StudyExhibit Stage(StudyModelData model, List<StudyExhibit> staged)
        {
            var obj = new GameObject("Exploration object");
            obj.transform.SetParent(transform, false);
            var exhibit = obj.AddComponent<StudyExhibit>();
            // TMP and XRI initialize in Awake. Build synchronously while active,
            // then hide before the next rendered frame until the whole edit is ready.
            staged.Add(exhibit); exhibit.Build(model);
            obj.SetActive(false);
            return exhibit;
        }

        private static void CopyPlacement(StudyExhibit source, StudyExhibit destination)
        {
            destination.Pose.SetPositionAndRotation(source.Pose.position, source.Pose.rotation);
            destination.Pose.localScale = source.Pose.localScale;
            if (source.Pane.IsPlaced) destination.Pane.Place(source.Pane.transform.position, source.Pane.transform.rotation);
        }
    }
}
