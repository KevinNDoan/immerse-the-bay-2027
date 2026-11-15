"""Validated edits to the learner's existing collection of study objects."""
from .nodes import _text
from .study_models import PLAN_SCHEMA, obj, validate_plan


ACTION_SCHEMA = obj({
    "action": {"type": "string", "enum": ["add", "revise", "remove"]},
    "targetId": {"type": "string"},
    "model": {"anyOf": [PLAN_SCHEMA, {"type": "null"}]},
})
REPLY_SCHEMA = obj({
    "action": {"type": "string", "enum": ["apply", "answer"]},
    "answer": {"type": "string"},
    "actions": {"type": "array", "items": ACTION_SCHEMA, "maxItems": 6},
})
ACTION_INSTRUCTIONS = """
You control the floating study objects through the actions you return. The app executes them.
You CAN create, edit, replace and remove these objects. Do not claim you lack that ability,
offer instructions instead of acting, or announce a change while returning an empty action list.
Requests phrased as 'can you', 'could you' or 'would you' are action requests when they ask you
to make a change. A conceptual question such as 'can this structure bend?' is still a question.
Use action=apply with one or more actions for requested changes; action=answer with actions=[]
for explanations or a short clarification when the intended existing object is ambiguous.
Each add has targetId="" and a complete model. Each revise has an existing targetId and a
complete replacement model preserving aspects the user did not ask to change. Each remove
has an existing targetId and model=null. IDs come only from objectsInSpace, never from titles.
Resolve explicitly named objects from the entire inventory, even if another object is selected.
Use selectedObjectId for 'this' or 'it'; if there is no unambiguous target, ask briefly.
A compound request can add a new object and remove an existing one in the same reply. For a
replacement of one selected object, revise is also appropriate. Do all parts of the request.
Never target the same existing ID twice in one reply. At most six objects may remain after all
actions; removals can free room for additions. Only remove objects the user asked to remove.
Keep unrelated objects. Do not create an object merely to answer an ordinary question.
Objects may explore a different subject from the current world. You change the floating study
objects, not the baked room mesh. Do not claim to remove architecture from that mesh.
Use compact models with meaningful parts and valid exploration steps. No topic-specific rules.
Explain the requested result concisely (under 550 characters); the app displays it after applying
all actions. If a request exceeds the available object capacity, ask which objects to replace.
"""


def validate_inventory(items, selected_id):
    if not isinstance(items, list) or len(items) > 6:
        raise ValueError("Invalid object inventory.")
    result, ids = [], set()
    for item in items:
        if not isinstance(item, dict):
            raise ValueError("Invalid object inventory.")
        identity = _text(item.get("id"), 64)
        if identity in ids:
            raise ValueError("Duplicate object ID.")
        ids.add(identity)
        result.append({"id": identity, "model": validate_plan(item.get("model"))})
    selected_id = _text(selected_id, 64, required=False)
    if selected_id and selected_id not in ids:
        raise ValueError("The selected object is no longer available.")
    return result, selected_id


def validate_reply(reply, inventory):
    if not isinstance(reply, dict):
        raise ValueError("Invalid object reply.")
    action = reply.get("action")
    actions = reply.get("actions")
    if not isinstance(actions, list) or len(actions) > 6:
        raise ValueError("Invalid object actions.")
    if action not in ("answer", "apply") or (action == "answer" and actions) or (action == "apply" and not actions):
        raise ValueError("Invalid object action result.")
    ids = {item["id"] for item in inventory}
    touched, clean = set(), []
    count = len(inventory)
    for item in actions:
        if not isinstance(item, dict):
            raise ValueError("Invalid object action.")
        kind = item.get("action")
        target = _text(item.get("targetId"), 64, required=False)
        if kind == "add":
            if target:
                raise ValueError("New objects cannot target an existing ID.")
            count += 1
        elif kind in ("revise", "remove"):
            if target not in ids or target in touched:
                raise ValueError("Invalid or repeated object target.")
            touched.add(target)
            if kind == "remove":
                count -= 1
        else:
            raise ValueError("Unsupported object action.")
        if kind == "remove":
            if item.get("model") is not None:
                raise ValueError("Removing an object cannot also create one.")
            model = None
        else:
            model = validate_plan(item.get("model"))
        clean.append({"action": kind, "targetId": target, "model": model})
    if count > 6:
        raise ValueError("This space can hold at most six objects.")
    return {"action": action, "answer": _text(reply.get("answer"), 800), "actions": clean, "model": None}
