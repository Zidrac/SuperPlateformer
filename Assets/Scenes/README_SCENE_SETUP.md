# Rebuild Scene — Platformer Rogue Lite (Quick Setup)

This kit gives you the minimal scripts and steps to get a playable scene back with run, jump, dash, lasso/grapple.

## 1) Place scripts
Unzip at your Unity project root (same level as `Packages/` and `ProjectSettings/`). It will create:

```
Assets/_Project/Scripts/
  AbilitySO.cs
  AbilityController.cs
  GroundedNotifier.cs
  SimpleJump.cs
  PlayerRun2D.cs
  AbilityInputBridge.cs
  CameraFollow2D.cs
  Abilities/
    DashAbilitySO.cs
    GrappleAbilitySO.cs
    LassoAbilitySO.cs
```

Unity will auto-import.

## 2) Create a test Scene
- Create a new Scene (e.g., `Assets/Scenes/Main.unity`) and open it.

## 3) Add a ground
- GameObject → 2D Object → Sprites → Square (scale X: 20, Y: 1) at y = -2
- Add **BoxCollider2D** to the ground
- Put it on a **Ground** layer (create it if needed).

## 4) Create the Player
- Create Empty `Player`
- Add components:
  - **Rigidbody2D** (Gravity Scale ~2.5, Collision detection: Continuous, Freeze Z Rotation)
  - **CapsuleCollider2D** or **BoxCollider2D**
  - **AbilityController**
  - **SimpleJump** (desired height ~3)
  - **PlayerRun2D**
  - **AbilityInputBridge**
- Under `Player`, create child `GroundCheck` (empty) at feet (y ≈ -0.9 from center)
  - Add **GroundedNotifier** on the child and assign:
    - Ability Controller: `Player`
    - GroundCheck: this child
    - Ground Layers: check the **Ground** layer.

## 5) Abilities (ScriptableObjects)
In Project window:
- Right Click → Create → Abilities → **Dash**
- Right Click → Create → Abilities → **Lasso**
- Right Click → Create → Abilities → **Grapple (Terraria-like)**
Open the `Player`:
- Assign two abilities in **AbilityController** (Slot A and Slot B). Example:
  - Slot A: Dash
  - Slot B: Lasso (or Grapple)
- Configure refill toggles as you like (refill on ground, reset cooldown, etc.)

## 6) Camera follow
- Select your main Camera
- Add **CameraFollow2D** and drag the `Player` into its Target field.

## 7) Inputs
- **Slot A** = Left Shift
- **Slot B** = E
- **Jump** = Space (already handled by `SimpleJump`)
- **Move** = A/D or Left/Right arrows

## 8) Play
Hit Play. You should be able to run, jump, dash (A), and lasso/grapple (B).