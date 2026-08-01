# Elemental GLB token import guide for coding LLMs

## Purpose

This guide explains how to reproduce and extend the working elemental GLB-token integration in this Unity project. It is written for another coding LLM, including DeepSeek, and focuses on the shortest reliable editor-only workflow.

The required result is:

- Unity imports every elemental `.glb` as a usable `GameObject` asset.
- All 16 gameplay tokens use the model assigned by their `PlayerStyle` in Play Mode.
- Red uses fire, blue uses water, yellow uses lightning, and green uses plant.
- Movement, ownership, selection, disabled-state feedback, and colliders keep working.
- No Windows, Android, or other player build is created.

The current implementation in the repository is the source of truth. Read the relevant files before changing it.

## Project facts

- Unity version: `6000.3.21f1`.
- Render pipeline: Universal Render Pipeline.
- Board plane: XY.
- Visual depth/height extends along negative Z.
- Models: `Assets/Models/fire.glb`, `WaterDrop.glb`, `lightning.glb`, and `plant.glb`.
- Shared token prefab: `Assets/Prefabs/Tokens/Token.prefab`.
- Four-colour token set: `Assets/Prefabs/Tokens/TokenSet.prefab`.
- Player styles: `Assets/Data/PlayerStyles/`.
- Main playable scene: `Assets/Scenes/LudoBoard3D.unity`.

## Why copying the GLB into Assets was not enough

Unity does not provide native GLB mesh import in this project. Before installing a glTF importer, Unity handled a `.glb` with `DefaultImporter`. This made the file visible in the Project window but did not produce a `GameObject`, mesh, hierarchy, or materials that a prefab could reference.

Adding an empty `MeshFilter` and `MeshRenderer` to the scene does not import the model. The previous object named `Fire Token (import manually)` was only an empty placeholder and was removed.

The solution is Unity glTFast, which registers a scripted importer for `.glb` and `.gltf` files. It performs editor-time import and exposes the result as a Unity `GameObject` asset. The project uses:

```json
"com.unity.cloud.gltfast": "6.19.0"
```

Official package documentation: <https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.19/manual/index.html>

## Fast implementation sequence

Follow this order. Do not start by manually editing a scene or fabricating a mesh reference.

### 1. Inspect and protect the current workspace

Run `git status` first. Preserve all unrelated changes. Confirm that every required `.glb` and its `.meta` file exist.

Do not delete or regenerate Unity `.meta` files. The GLB currently has GUID:

```text
2bfe5d99449b3ec4c99debcaa34e885c
```

The GUID identifies the source asset. The imported local file ID should be obtained from Unity rather than guessed, because it can change if the asset or importer changes.

### 2. Install the GLB importer

Add glTFast to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.cloud.gltfast": "6.19.0"
  }
}
```

Keep all existing dependencies; only add the new entry in the existing object.

Let Unity Package Manager update `Packages/packages-lock.json`. Do not construct the lock entry manually. The resolved package also changes the dependency depth of Burst, Collections, Mathematics, Mono Cecil, and the performance-test package. Those resolver changes are expected.

If the Editor is already open, do not launch a second Unity instance against the same project. Allow the open Editor to refresh. Bringing the window into focus can trigger the normal refresh if Auto Refresh is focus-based.

Successful import can be confirmed in the Unity Editor log by an entry similar to:

```text
Start importing Assets/Models/fire.glb ... (ScriptedImporter)
```

`AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/fire.glb")` must return a non-null object.

Important: the text in `fire.glb.meta` can still look like a `DefaultImporter` entry. Do not use that text as the only test. Verify the loaded asset type or the Editor log after glTFast is installed.

### 3. Make custom models part of player style data

Do not create separate gameplay logic for red tokens. Extend the existing `PlayerStyle` ScriptableObject with optional presentation data:

```csharp
[Header("Optional token model")]
[SerializeField] private GameObject tokenModel;
[SerializeField] private Vector3 tokenModelEulerAngles =
    new Vector3(-90f, 0f, 0f);
[SerializeField, Min(0.01f)] private float tokenModelFootprint = 0.68f;
[SerializeField, Min(0.01f)] private float tokenModelHeight = 0.98f;
[SerializeField] private Color tokenModelTint = Color.white;
[SerializeField] private TokenModelMaterialMode tokenModelMaterialMode;
```

Expose read-only properties for those fields. A null `tokenModel` means that player continues using the procedural pawn.

This keeps model choice data-driven and reusable. Assign a different model to a player-style asset without duplicating token movement code.

### 4. Pass the whole style to the visual component

In `Token.RefreshVisual()`, replace the colour-only call with:

```csharp
visual.SetStyle(ownerStyle);
```

Use `visual.SetStyle(null)` for a missing style. `Token` remains responsible for gameplay state and ownership; `TokenVisual` remains responsible for presentation.

### 5. Instantiate and fit the model in TokenVisual

`TokenVisual.SetStyle` must:

1. Keep the player colour for the procedural fallback.
2. Read the optional model, fitting, tint, and opacity values from `PlayerStyle`.
3. In Play Mode, instantiate the model as a child of the existing token visual.
4. Disable the procedural `MeshRenderer` when a custom model exists.
5. Leave the root `Token` component and its `CapsuleCollider` untouched.
6. Disable any colliders imported inside the GLB so there is only one gameplay hit target.
7. Copy the token layer recursively to the imported hierarchy.
8. Preserve imported materials by default and use `MaterialPropertyBlock` for selectable/disabled feedback.
9. Apply the style's **Token Model Material Mode** to runtime-only material clones: use opaque for solid models and alpha clip for textures that need transparent cutouts.

The runtime child is named:

```text
__TokenModel
```

Do not instantiate the model from `OnValidate` while Unity is importing persistent prefab assets. The safe guard used here is:

```csharp
if (!Application.isPlaying)
{
    return;
}
```

Therefore the procedural red pawn remains visible as a lightweight edit-time placeholder. It switches to the fire model when Play Mode starts.

### 6. Orient and auto-fit the model

The imported GLB uses the usual 3D vertical axis, but this board lies in XY and projects pieces along negative Z. Rotate the model by `-90` degrees around X.

Do not rely on the source model's absolute scale or pivot. Compute bounds from every child `Renderer`, convert the corners from renderer-local space into `TokenVisual` space, and combine them.

Use a uniform scale:

```text
scale = min(targetFootprint / max(bounds.size.x, bounds.size.y),
            targetHeight / bounds.size.z)
```

After scaling:

- Centre the model in local X and Y.
- Offset local Z so `bounds.max.z` is at zero.
- Use target footprint `0.68` and height `0.98` for the fire token.

This matches the existing pawn and keeps the model usable in home circles and route cells.

### 7. Preserve interaction-state feedback

The imported model can contain several renderers and materials. Do not clone or permanently modify its materials.

For each renderer/material slot:

- Read `_BaseColor`, falling back to `_Color` or white.
- Normal state: use the material's base colour.
- Selectable state: lerp the base colour toward white by `0.32`.
- Disabled state: lerp toward `(0.2, 0.2, 0.2)` by `0.58`.
- Apply the value through `MaterialPropertyBlock` for that material index.

This retains the GLB appearance while keeping the gameplay selection cues.

### 8. Assign each model to its PlayerStyle

Preferred method: select each style asset in Unity and assign its model in the **Token Model** field. The current assignments are:

| Player style | Model | Euler angles | Height | Tint | Material mode |
| --- | --- | --- | --- | --- | --- |
| `RedPlayerStyle` | `fire.glb` | `(-90, 0, 0)` | `0.98` | White | Opaque |
| `BluePlayerStyle` | `WaterDrop.glb` | `(180, 0, 0)` | `1.15` | Dark blue `(0.08, 0.25, 0.45, 1)` | Opaque |
| `YellowPlayerStyle` | `lightning.glb` | `(-90, 0, 0)` | `0.98` | White | Opaque |
| `GreenPlayerStyle` | `plant.glb` | `(180, 0, 0)` | `0.98` | White | Alpha Clip |

All four currently use the same footprint target:

```text
Token Model Footprint: 0.68
```

The water model is taller because its source shape is a narrow drop. Rotate it
180 degrees on X so its rounded base rests on the board and its point faces up.
It is also forced opaque because `WaterDrop.glb` declares both `alphaMode: BLEND`
and full `KHR_materials_transmission`; leaving that glass configuration unchanged
makes the token disappear against parts of the board at some camera angles.
The plant also declares `alphaMode: BLEND`, but its texture contains transparent
leaf cutouts. Use alpha clipping rather than plain opaque rendering so it writes
depth without drawing the texture's transparent background. Its verified Euler
rotation is `(180, 0, 0)`; the former `(-90, 0, 0)` placed the leaf rosette on its
side and made the whole model collapse to a thin silhouette from two camera
directions. Fire and lightning are kept opaque, and all runtime material modes
explicitly render both sides.

For LLM automation, use a temporary editor script instead of hand-writing an unknown file ID:

```csharp
GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Models/fire.glb");
PlayerStyle style = AssetDatabase.LoadAssetAtPath<PlayerStyle>(
    "Assets/Data/PlayerStyles/RedPlayerStyle.asset");

SerializedObject serializedStyle = new SerializedObject(style);
serializedStyle.FindProperty("tokenModel").objectReferenceValue = model;
serializedStyle.FindProperty("tokenModelEulerAngles").vector3Value =
    new Vector3(-90f, 0f, 0f);
serializedStyle.FindProperty("tokenModelFootprint").floatValue = 0.68f;
serializedStyle.FindProperty("tokenModelHeight").floatValue = 0.98f;
serializedStyle.FindProperty("tokenModelMaterialMode").enumValueIndex =
    (int)TokenModelMaterialMode.Opaque;
serializedStyle.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(style);
AssetDatabase.SaveAssets();
```

Run this only after glTFast has imported the GLB and the model is non-null. Remove the temporary editor script and its `.meta` file afterward. Do not leave an `InitializeOnLoad` script that overwrites future Inspector changes on every domain reload.

For example, the resulting red serialized reference in this revision is:

```yaml
tokenModel: {fileID: 8128668236925612103, guid: 2bfe5d99449b3ec4c99debcaa34e885c, type: 3}
```

Load and serialize the reference through Unity whenever possible; do not assume this file ID is universal.

### 9. Remove obsolete scene-only experiments

Remove the empty root object named `Fire Token (import manually)` from `LudoBoard3D.unity`. The actual model is now selected by `RedPlayerStyle` and is instantiated for all four red tokens. A separate decorative scene object is incorrect and can be confused with a gameplay token.

If `LudoBoard3D.unity` was already open and dirty when its YAML was changed externally, Unity can continue entering Play Mode from `Temp/__Backupscenes/0.backup`. That in-memory backup can retain `Fire Token (import manually)` even after the committed scene file no longer contains it. Do not save the obsolete object back into the scene. Either reload the scene from disk after preserving any legitimate unsaved work, or rely on the current Play Mode safeguard, which removes that exact legacy root before gameplay begins.

An old experimental copy can also have a different name, parent, scale, or slightly lower Z position. The current implementation therefore performs an additional mesh-identity check after each legitimate custom model is created. Any renderer in the same scene that uses the imported token mesh but is not beneath a `TokenVisual`'s active `customModelInstance` is disabled. This removes the smaller fire visible underneath a real token without depending on the stale object's name.

## Verification without creating a build

Do not compile an EXE, APK, or other player merely to validate this task.

Use this verification sequence:

1. Confirm Unity reports no C# compiler errors.
2. Confirm the Editor log imports every assigned GLB with `ScriptedImporter`.
3. Confirm all four GLBs load as `GameObject` assets through glTFast.
4. Open `Assets/Scenes/LudoBoard3D.unity`.
5. Enter Play Mode.
6. Confirm there are four tokens for each of the four colours.
7. Confirm every token has exactly one child named `__TokenModel` with at least one enabled renderer.
8. Confirm the procedural renderer on each `TokenVisual` is disabled only at runtime.
9. Test the top, isometric, and perspective presets at their supported minimum and maximum zoom values. Confirm model bounds remain inside the camera near/far clipping range and that tokens in the viewport remain visible. Rotate around every token model and confirm it remains visible from every angle.
10. Roll a 5 and take an elemental token out of Home.
11. Confirm the model moves with the token and no static copy remains in its former Home position.
12. Select a token to confirm the existing root collider and interaction state still work.
13. Exit Play Mode and run `git diff --check`.

The automated smoke test used during the original implementation reported:

```text
RED_TOKEN_VALIDATION: PASS - all four red tokens use fire.glb.
```

The four-model editor validation subsequently confirmed that every `PlayerStyle`
resolves to its intended GLB, each imported model has a supported renderer and
material, all fitted bounds remain within the configured token envelope and in
front of the raised board, and every model projects to visible pixels at the
maximum zoom-out of the top, isometric, and perspective views.

Any temporary validator must survive Unity's Play Mode domain reload. Store its phase in `SessionState`, validate after entering Play Mode, exit Play Mode, and then delete the validator and its `.meta` file.

## Failed approaches and traps to avoid

### Empty MeshFilter placeholder

Creating a scene object with `MeshFilter` and `MeshRenderer` does not read a GLB file. Install a real importer first.

### Instantiating during OnValidate

The initial preview attempt instantiated the model while persistent prefab assets were being validated. Unity emitted errors such as:

```text
Cannot instantiate objects with a parent which is persistent.
SendMessage cannot be called during Awake, CheckConsistency, or OnValidate.
```

Instantiate the GLB only during Play Mode unless a dedicated editor-safe preview system is implemented with delayed editor callbacks and proper prefab-stage handling.

That failed preview attempt can leave hidden, unparented objects named `__TokenModel` in the current Editor session. They appear as static fire copies after the real token moves away. Reloading the scene or restarting Unity removes `DontSaveInEditor` leftovers. The current `TokenVisual` also performs a defensive Play Mode cleanup: once per loaded scene it destroys only `__TokenModel` scene roots that have no owning `TokenVisual`, plus the exact obsolete root `Fire Token (import manually)` that can survive in Unity's dirty scene backup. Never remove the child model that is parented beneath a legitimate token.

If the extra copy was renamed or nested, compare renderer mesh references rather than transforms or names. A legitimate renderer must be below the `customModelInstance` owned by its nearest `TokenVisual`. Disable any renderer using the same imported mesh that fails that ownership check. In a clean home-exit reproduction there are exactly four fire renderers before and after movement, all four have token owners, and none remains near the selected token's former Home position.

### Saving runtime renderer state into TokenSet.prefab

An editor validation pass can save `m_Enabled: 0` overrides for procedural renderers in `TokenSet.prefab`. Those overrides must be removed for all 16 tokens. The renderer must be enabled in the shared prefab and disabled dynamically only while a custom model exists in Play Mode.

### Replacing the complete token object

Do not replace the root token with the imported GLB. Doing so risks losing `Token`, ownership data, route state, the root collider, and selection logic. Replace only the presentation under `TokenVisual`.

### Editing shared materials

Do not change imported shared materials directly to display selectable or disabled states. That can affect all instances. Use `MaterialPropertyBlock`.

`WaterDrop.glb` uses full transmission and becomes nearly invisible depending on
the camera and background. `plant.glb` uses blended transparency for a texture
with real transparent cutouts and can suffer from depth-sorting disappearance.
`TokenVisual` therefore creates runtime-only material clones according to
`PlayerStyle.TokenModelMaterialMode`: opaque clones for fire, water, and
lightning; depth-writing alpha-clipped clones for plant. Both modes explicitly
disable back-face culling. Source material assets remain unchanged, and the
clones are destroyed with the token visual.

### Confusing unrelated console warnings

This branch can emit existing `LudoBoardRenderer.BuildCellLabels` warnings from its `OnValidate` workflow. They are separate from the GLB token integration. Validate compiler errors and token-specific logs independently; do not expand this task into an unrelated board-renderer refactor.

### Leaving temporary automation in the repository

Delete temporary setup and validation scripts, their `.meta` files, and any empty `Assets/Editor` metadata they created. Never commit `Library`, `Temp`, editor logs, or generated IDE files.

## Files that should change

- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Scripts/Tokens/PlayerStyle.cs`
- `Assets/Scripts/Tokens/Token.cs`
- `Assets/Scripts/Tokens/TokenVisual.cs`
- `Assets/Data/PlayerStyles/*.asset` for each player receiving a model
- The corresponding `.glb` and `.glb.meta` files in `Assets/Models/`
- `Assets/Scenes/LudoBoard3D.unity` only to remove the obsolete placeholder

`TokenSet.prefab` should not retain custom renderer-enabled overrides from editor automation.

## Completion checklist for the next LLM

- [ ] Read the current token scripts and player-style assets.
- [ ] Preserve unrelated working-tree changes.
- [ ] Install glTFast through `Packages/manifest.json`.
- [ ] Let Unity resolve `packages-lock.json`.
- [ ] Confirm every assigned GLB loads as a `GameObject`.
- [ ] Keep model selection in `PlayerStyle`.
- [ ] Keep gameplay logic and the root collider on the existing token.
- [ ] Instantiate, rotate, centre, and uniformly fit the model in Play Mode.
- [ ] Preserve imported materials and interaction feedback, except for explicitly configured runtime-only opaque or alpha-clipped clones.
- [ ] Assign models through the four existing player-style assets.
- [ ] Remove the empty manual scene placeholder.
- [ ] Run an editor compilation and Play Mode smoke test.
- [ ] Move each type of elemental token and verify no static copy remains.
- [ ] Do not create a player build.
- [ ] Remove temporary editor scripts and accidental prefab overrides.
- [ ] Review `git diff` and `git diff --check` before reporting completion.
