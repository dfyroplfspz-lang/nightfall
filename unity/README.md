# NIGHTFALL — Unity port

The pygame build of NIGHTFALL, ported to Unity. Same game: 5 modes, 5 maps,
4 weapons with their own models, 24 skins, cases, and a torch that is the only
thing in the arena that shows you a zombie.

No art or audio files ship with the project. Every shape is drawn as vector
geometry at runtime and every sound is synthesized on load, exactly like the
original.

## Running it

1. Install Unity Hub, then any Unity **2022.3 LTS or newer** editor.
2. In Unity Hub: `Add` → pick this `unity` folder → open it.
3. On first open the project writes `Assets/Scenes/Main.unity` for you and sets
   the product name and resolution. Open that scene and press **Play**.
4. `F11` toggles fullscreen, `Esc` pauses, `F` kills the torch.

Building a standalone: `File → Build Settings → Windows → Build`. The scene is
already in the build list.

## Layout

```
Assets/Scripts/Core/      settings, maps, modes, skins, weapon and gun data
Assets/Scripts/Gameplay/  arena, player, zombies, pickups, particles, the loop
Assets/Scripts/Render/    the vector painter, the night overlay, text
Assets/Scripts/UI/        menu, armory, cases, HUD, splash and loader
Assets/Scripts/Audio/     the synthesizer
Assets/Resources/Shaders/ three tiny unlit shaders
Assets/Editor/            one-time project setup
```

The game boots itself from `Bootstrap.cs`, so it runs from any open scene.

BIG GAMES STUDIO
