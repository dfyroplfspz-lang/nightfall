// C# / Assets/Scripts/Gameplay/GameApp.cs / Unity 2022.3+ / states, loop, camera, lighting
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public enum GameState { Splash, Loading, Menu, Playing, Break, Paused, GameOver }

    [DisallowMultipleComponent]
    public class GameApp : MonoBehaviour
    {
        // ---- services ------------------------------------------------------
        public ProcAudio audio;
        public SaveData save;
        public Inventory inventory;
        public bool inventoryDirty;
        public Menu menu;
        public Rng rng = new Rng(12345);

        Camera _cam;
        Camera _background;
        Boot.Splash _splash;
        Boot.Loader _loader;

        // ---- run state -----------------------------------------------------
        public GameState state = GameState.Splash;
        GameState _resumeState = GameState.Playing;

        public GameMode mode = Modes.All[0];
        public MapDef mapdef = Maps.All[0];
        public Arena world;
        public Player player;
        public ParticleManager particles;
        public readonly List<Zombie> zombies = new List<Zombie>();
        public readonly List<Bullet> bullets = new List<Bullet>();
        public readonly List<Bullet> enemyBullets = new List<Bullet>();
        public readonly List<Pickup> pickups = new List<Pickup>();
        Grid _grid = new Grid(96);
        readonly List<Zombie> _aliveBuf = new List<Zombie>(S.MAX_ALIVE + 8);

        public int wave;
        public int score;
        public float timeAlive;
        readonly List<string> _spawnQueue = new List<string>();
        float _spawnTimer;
        public float breakTimer = 3f;
        public float timeLeft;
        string _banner = "", _bannerSub = "";
        float _bannerT;
        float _shakeMag;
        int _pickupsTaken;
        float _hurtFlash;
        float _muzzleFlash;
        Vector2 _cameraPos;
        Vector2 _drawCam;
        int _runScrap;
        float _menuT;

        // ---- input ---------------------------------------------------------
        /// <summary>
        /// One frame of intent, decoupled from UnityEngine.Input so the self-test can
        /// script a whole run without a window.
        /// </summary>
        public struct InputFrame
        {
            public Vector2 wish;
            public Vector2 mouse;
            public bool fireHeld, firePressed;
            public bool dash, reload, toggleTorch, nextWeapon, prevWeapon;
            public int selectSlot;      // -1 for none
        }

        InputFrame _input;
        Vector2 _wish;
        Vector2 _mouse;
        bool _fireHeld, _firePressed;
        bool _booted;

        // ------------------------------------------------------------- setup
        /// <summary>Set by the self-test before it adds the component, so Awake skips
        /// cameras, audio hardware and the save file.</summary>
        public static bool HeadlessRequested;
        public static int HeadlessSeed = 7;

        void Awake()
        {
            if (HeadlessRequested) BootHeadless(HeadlessSeed);
            else BootFull();
        }

        /// <summary>
        /// Everything the game needs before the first frame. Named BootFull so it does
        /// not shadow the Boot screen class in this namespace.
        /// </summary>
        public void BootFull()
        {
            if (_booted) return;
            _booted = true;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Screen.fullScreen = true;

            _background = gameObject.AddComponent<Camera>();
            _background.clearFlags = CameraClearFlags.SolidColor;
            _background.backgroundColor = Color.black;
            _background.cullingMask = 0;
            _background.depth = -10;
            _background.orthographic = true;

            var camGo = new GameObject("NightfallCamera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = S.C_BG;
            _cam.cullingMask = 0;
            _cam.depth = 0;
            _cam.orthographic = true;
            camGo.AddComponent<PainterCamera>().app = this;

            Painter.EnsureMaterials();

            audio = new ProcAudio(gameObject);
            save = SaveData.Load();
            inventory = save.ToInventory();
            rng = new Rng(unchecked((int)System.DateTime.Now.Ticks));
            menu = new Menu(save, inventory, rng);

            _splash = new Boot.Splash(audio);
            BuildLoader();

            Reset(null, null);
            UnityEngine.Cursor.visible = false;
        }

        /// <summary>Headless variant: no cameras, no audio hardware, no cursor.</summary>
        public void BootHeadless(int seed)
        {
            if (_booted) return;
            _booted = true;
            headless = true;

            audio = new ProcAudio();
            save = new SaveData();
            inventory = save.ToInventory();
            rng = new Rng(seed);
            menu = new Menu(save, inventory, rng);
            Reset(null, null);
        }

        /// <summary>True while the self-test drives the game, so nothing touches the GPU.</summary>
        public bool headless;

        void BuildLoader()
        {
            var steps = new List<Boot.Step>
            {
                new Boot.Step("warming up the night", () =>
                {
                    // build the styles up front; measuring needs OnGUI, so it waits
                    Txt.Style(92, true);
                    Txt.Style(26, true);
                    Txt.Style(19, false);
                }),
                new Boot.Step("synthesizing audio", () => audio.Play("ui", 0f))
            };

            foreach (var m in Maps.All)
            {
                MapDef captured = m;
                steps.Add(new Boot.Step("drawing " + captured.name.ToLower(),
                                        () => Maps.PreviewLayout(captured)));
            }

            steps.Add(new Boot.Step("unlocking the armory", null));
            steps.Add(new Boot.Step("baking the arena", () => Reset(null, null)));
            _loader = new Boot.Loader(steps, 11);
        }

        public void Reset(GameMode newMode, MapDef newMap)
        {
            int seed = (int)(rng.NextUInt() & 0x3fffffff);
            if (newMode != null) mode = newMode;
            if (newMap != null) mapdef = newMap;

            world = new Arena(seed, mapdef);

            // metro puts a pillar on the world centre and park puts a fountain there,
            // so the spawn looks for clear ground instead of trusting the midpoint
            Vector2 start = world.center;
            if (world.Blocked(start, S.P_RADIUS + 6f))
                start = world.FreePoint(rng, world.center, 0f, 460f, S.P_RADIUS + 10f);
            player = new Player(start);
            particles = new ParticleManager(world, seed);

            zombies.Clear();
            bullets.Clear();
            enemyBullets.Clear();
            pickups.Clear();
            _spawnQueue.Clear();
            _grid = new Grid(96);

            wave = 0;
            score = 0;
            timeAlive = 0f;
            _spawnTimer = 0f;
            breakTimer = 3f;
            timeLeft = mode.timeLimit;
            _banner = "";
            _bannerSub = "";
            _bannerT = 0f;
            _shakeMag = 0f;
            _pickupsTaken = 0;
            _hurtFlash = 0f;
            _muzzleFlash = 0f;
            UpdateCamera(0f, true);
        }

        public void StartRun(GameMode newMode, MapDef newMap)
        {
            Reset(newMode, newMap);
            player.skin = inventory.PlayerSkin;
            player.weaponSkin = inventory.WeaponSkin;

            state = GameState.Break;
            breakTimer = 3f;
            wave = 0;
            player.maxHp = mode.playerHp;
            player.hp = mode.playerHp;

            for (int i = 0; i < mode.startWeapons.Length; i++)
                player.GiveWeapon(mode.startWeapons[i]);
            player.Select(0);

            _banner = mode.name;
            _bannerSub = mode.tagline;
            _bannerT = 2f;
            audio.Play("ui");
        }

        // ----------------------------------------------------------- helpers
        public int ZombiesLeft() { return zombies.Count + _spawnQueue.Count; }

        /// <summary>Top-left of the view in world space, for tests that aim by hand.</summary>
        public Vector2 CameraPos { get { return _cameraPos; } }

        void Shake(float amount) { _shakeMag = Mathf.Min(26f, _shakeMag + amount); }

        Vector2 ScreenToWorld(Vector2 p) { return p + _cameraPos; }

        void UpdateCamera(float dt, bool instant = false)
        {
            Vector2 target = new Vector2(player.pos.x - S.WIDTH * 0.5f,
                                         player.pos.y - S.HEIGHT * 0.5f);
            target += player.aim * 46f;      // lead the camera for better framing
            target.x = MathUtil.Clamp(target.x, 0f, Mathf.Max(0f, S.WORLD_W - S.WIDTH));
            target.y = MathUtil.Clamp(target.y, 0f, Mathf.Max(0f, S.WORLD_H - S.HEIGHT));

            if (instant) _cameraPos = target;
            else _cameraPos += (target - _cameraPos) * Mathf.Min(1f, 9f * dt);
        }

        public void Persist()
        {
            if (headless) return;
            save.FromInventory(inventory);
            save.Store();
            inventoryDirty = false;
        }

        // ------------------------------------------------------------- loop
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Letterbox.Recalculate();
            Letterbox.ApplyTo(_cam);
            _mouse = Letterbox.ToVirtual(Input.mousePosition);

            if (Input.GetKeyDown(KeyCode.F11))
                Screen.fullScreen = !Screen.fullScreen;
            if (Input.GetKeyDown(KeyCode.M))
                audio.ToggleMute();

            switch (state)
            {
                case GameState.Splash: UpdateSplash(dt); return;
                case GameState.Loading: UpdateLoading(dt); return;
                case GameState.Menu: UpdateMenu(dt); return;
                default: UpdateGame(dt); return;
            }
        }

        void UpdateSplash(float dt)
        {
            if (Input.anyKeyDown) _splash.Skip();
            _splash.Update(dt);
            if (_splash.done) state = GameState.Loading;
        }

        void UpdateLoading(float dt)
        {
            _loader.Update(dt);
            _loader.RunOne();
            if (_loader.Done) state = GameState.Menu;
        }

        void UpdateMenu(float dt)
        {
            _menuT += dt;
            menu.Update(dt, _mouse);

            string outMode, outMap;
            string action = null;

            foreach (KeyCode k in MenuKeys)
                if (Input.GetKeyDown(k))
                {
                    action = menu.HandleKey(k, out outMode, out outMap);
                    MenuAction(action, outMode, outMap);
                    return;
                }

            if (Input.GetMouseButtonDown(0))
            {
                action = menu.HandleClick(_mouse, out outMode, out outMap);
                MenuAction(action, outMode, outMap);
            }
        }

        static readonly KeyCode[] MenuKeys =
        {
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space,
            KeyCode.Escape, KeyCode.Backspace
        };

        void MenuAction(string action, string modeKey, string mapKey)
        {
            if (string.IsNullOrEmpty(action)) return;
            switch (action)
            {
                case "blip": audio.Play("ui", 0.5f); break;
                case "deny": audio.Play("empty", 0.5f); break;
                case "save": audio.Play("pickup", 0.5f); Persist(); break;
                case "quit": Quit(); break;
                case "start":
                    StartRun(Modes.Get(modeKey), Maps.Get(mapKey));
                    break;
            }
        }

        void Quit()
        {
            Persist();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------- game update
        InputFrame ReadGameInput()
        {
            var inp = new InputFrame { selectSlot = -1, mouse = _mouse };

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inp.wish.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inp.wish.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inp.wish.y -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inp.wish.y += 1f;

            inp.fireHeld = Input.GetMouseButton(0);
            inp.firePressed = Input.GetMouseButtonDown(0);
            inp.dash = Input.GetKeyDown(KeyCode.Space);
            inp.reload = Input.GetKeyDown(KeyCode.R);
            inp.toggleTorch = Input.GetKeyDown(KeyCode.F);

            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            inp.nextWeapon = Input.GetKeyDown(KeyCode.E) || wheel > 0.01f;
            inp.prevWeapon = Input.GetKeyDown(KeyCode.Q) || wheel < -0.01f;

            if (Input.GetKeyDown(KeyCode.Alpha1)) inp.selectSlot = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) inp.selectSlot = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) inp.selectSlot = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) inp.selectSlot = 3;

            return inp;
        }

        void UpdateGame(float dt)
        {
            _input = ReadGameInput();
            _wish = _input.wish;
            _fireHeld = _input.fireHeld;
            _firePressed = _input.firePressed;
            _menuT += dt;

            if (state == GameState.GameOver)
            {
                particles.Update(dt);
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                    StartRun(null, null);
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    state = GameState.Menu;
                    menu.screen = "main";
                }
                return;
            }

            if (state == GameState.Paused)
            {
                particles.Update(dt);
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                    state = _resumeState;
                else if (Input.GetKeyDown(KeyCode.R)) StartRun(null, null);
                else if (Input.GetKeyDown(KeyCode.Q))
                {
                    state = GameState.Menu;
                    menu.screen = "main";
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                _resumeState = state;
                state = GameState.Paused;
                audio.Play("ui");
                return;
            }

            SimulateFrame(dt, _input);
        }

        /// <summary>
        /// One frame of the run, driven purely by the given intent. The editor feeds it
        /// the keyboard and mouse; the self-test feeds it a script.
        /// </summary>
        public void SimulateFrame(float dt, InputFrame inp)
        {
            _input = inp;
            _wish = inp.wish;
            _mouse = inp.mouse;
            _fireHeld = inp.fireHeld;
            _firePressed = inp.firePressed;

            ApplyActions(inp);

            Player p = player;
            timeAlive += dt;
            _bannerT = Mathf.Max(0f, _bannerT - dt);
            _shakeMag *= Mathf.Max(0f, 1f - 7f * dt);
            _hurtFlash = Mathf.Max(0f, _hurtFlash - dt * 2.4f);
            _muzzleFlash = Mathf.Max(0f, _muzzleFlash - dt * 7f);

            p.AimAt(ScreenToWorld(_mouse));
            p.HandleMove(_wish, dt, world);
            p.Update(dt);

            Fire();
            RebuildGrid();
            UpdateBullets(dt);
            UpdateZombies(dt);
            UpdatePickups(dt);
            particles.Update(dt);
            UpdateWaves(dt);
            UpdateCamera(dt);

            if (mode.timeLimit > 0f)
            {
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    EndRun();
                    return;
                }
            }

            if (!p.alive) EndRun();
        }

        void ApplyActions(InputFrame inp)
        {
            if (inp.reload && player.weapon.StartReload()) audio.Play("reload", 0.7f);

            if (inp.dash && player.TryDash(inp.wish))
            {
                audio.Play("dash", 0.6f);
                particles.Smoke(player.pos, 5);
            }

            if (inp.selectSlot >= 0) player.Select(inp.selectSlot);

            if (inp.toggleTorch)
            {
                player.flashlightOn = !player.flashlightOn;
                audio.Play("ui", 0.5f);
            }

            if (inp.nextWeapon) player.NextWeapon(1);
            if (inp.prevWeapon) player.NextWeapon(-1);
        }

        void RebuildGrid()
        {
            _aliveBuf.Clear();
            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i].alive) _aliveBuf.Add(zombies[i]);
            _grid.Build(_aliveBuf);
        }

        // one cached comparison so the per-bullet sort never allocates a delegate
        static Vector2 _sortOrigin;
        static readonly System.Comparison<Zombie> _byDistance = (x, y) =>
            (x.pos - _sortOrigin).sqrMagnitude.CompareTo((y.pos - _sortOrigin).sqrMagnitude);

        // ---- shooting ------------------------------------------------------
        void Fire()
        {
            Player p = player;
            List<Bullet> fresh = p.Shoot(_firePressed, _fireHeld, rng);
            if (fresh == null || fresh.Count == 0)
            {
                if (_firePressed && p.weapon.inMag == 0 && p.weapon.reloading <= 0f)
                {
                    audio.Play("empty", 0.5f);
                    p.weapon.StartReload();
                }
                return;
            }

            WeaponSpec spec = p.weapon.spec;
            float mult = p.damageBoost > 0f ? 1.5f : 1f;
            for (int i = 0; i < fresh.Count; i++)
            {
                fresh[i].damage *= mult;
                bullets.Add(fresh[i]);
            }

            audio.Play(spec.sound, 0.75f);
            particles.Muzzle(p.MuzzlePos(), p.aim, spec.key == "shotgun" ? 1.6f : 1f);
            Shake(spec.shake);
            _muzzleFlash = 1f;
        }

        void UpdateBullets(float dt)
        {
            Player p = player;

            for (int bi = bullets.Count - 1; bi >= 0; bi--)
            {
                Bullet b = bullets[bi];
                Vector2? wall = b.Update(dt, world);
                if (wall.HasValue)
                {
                    b.pos = wall.Value;
                    particles.Sparks(wall.Value, 6);
                }

                var cands = _grid.QuerySegment(b.prev, b.pos, 50f);
                _sortOrigin = b.prev;
                cands.Sort(_byDistance);

                for (int i = 0; i < cands.Count; i++)
                {
                    Zombie z = cands[i];
                    if (!z.alive || b.hits.Contains(z)) continue;

                    float? t = MathUtil.SegCircleHit(b.prev, b.pos, z.pos, z.radius);
                    if (!t.HasValue) continue;

                    b.hits.Add(z);
                    Vector2 point = b.prev + (b.pos - b.prev) * t.Value;
                    Vector2 dir = MathUtil.Norm(b.vel);
                    bool dead = z.Hurt(b.damage, dir, p.weapon.spec.knockback);
                    p.hits++;

                    particles.Blood(point, dir, 8);
                    world.Splat(point, 3, 12f);
                    audio.Play("hit", 0.45f);

                    if (z.spec.elite)
                        particles.Text(point, ((int)b.damage).ToString(), S.C_YELLOW, 18);
                    if (dead) Kill(z, dir);

                    if (b.pierce > 0) b.pierce--;
                    else { b.alive = false; break; }
                }

                if (!b.alive) bullets.RemoveAt(bi);
            }

            // acid spit
            for (int bi = enemyBullets.Count - 1; bi >= 0; bi--)
            {
                Bullet b = enemyBullets[bi];
                Vector2? wall = b.Update(dt, world);
                if (wall.HasValue)
                {
                    particles.Blood(wall.Value, null, 6, 140f, S.Col(120, 190, 90));
                }
                else if (MathUtil.SegCircleHit(b.prev, b.pos, p.pos, p.radius).HasValue)
                {
                    b.alive = false;
                    if (p.TakeDamage(b.damage)) OnPlayerHurt(b.damage);
                    particles.Blood(p.pos, null, 8, 160f, S.Col(120, 190, 90));
                }

                if (!b.alive) enemyBullets.RemoveAt(bi);
            }
        }

        void Kill(Zombie z, Vector2 direction)
        {
            player.kills++;
            score += (int)((z.spec.score + wave) * mode.scoreMult);
            particles.Gore(z.pos, z.spec.elite ? 40 : 20);
            audio.Play("death", 0.5f);

            if (z.spec.key == "boss")
            {
                particles.Explosion(z.pos);
                world.Scorch(z.pos, 90f);
                audio.Play("explode", 0.9f);
                Shake(18f);
                score += 250;
                particles.Text(z.pos, "BOSS DOWN", S.C_ORANGE, 30);
            }

            if (mode.timeLimit > 0f) timeLeft += 1f;

            float caseChance = 0.022f * mode.dropMult + (z.spec.elite ? 0.004f : 0f);
            if (rng.Value < caseChance)
            {
                var c = new Pickup(z.pos, "case");
                c.caseKey = Skins.DropCaseKey(rng, wave);
                pickups.Add(c);
            }
            else if (rng.Value < mode.dropMult)
            {
                Pickup drop = Pickup.RollDrop(z.pos, rng, player, wave);
                if (drop != null) pickups.Add(drop);
            }
        }

        // ---- zombies -------------------------------------------------------
        void UpdateZombies(float dt)
        {
            for (int i = zombies.Count - 1; i >= 0; i--)
                if (!zombies[i].Update(dt, player, world, _grid, this))
                    zombies.RemoveAt(i);
        }

        public void OnPlayerHurt(float amount)
        {
            _hurtFlash = 1f;
            Shake(6.5f);
            audio.Play("hurt", 0.8f);
            particles.Blood(player.pos, null, 8, 120f);
            world.Splat(player.pos, 3, 14f);
        }

        public void BossSummon(Zombie boss)
        {
            float hp, sp, dmg;
            ZombieTypes.WaveScales(wave, out hp, out sp, out dmg);
            for (int i = 0; i < 3; i++)
            {
                float ang = rng.Range(0f, 360f);
                Vector2 pos = boss.pos + MathUtil.FromAngle(ang, boss.radius + 40f);
                world.Collide(ref pos, 15f);
                zombies.Add(new Zombie(ZombieTypes.Get("walker"), pos, rng, hp, sp, dmg));
            }
            particles.Smoke(boss.pos, 10);
        }

        // ---- pickups -------------------------------------------------------
        void UpdatePickups(float dt)
        {
            Player p = player;
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pk = pickups[i];
                pk.Update(dt);

                if (pk.alive && (pk.pos - p.pos).magnitude < pk.radius + p.radius)
                {
                    string msg = pk.Apply(p, this);
                    if (msg != null)
                    {
                        pk.alive = false;
                        audio.Play("pickup", 0.7f);
                        _pickupsTaken++;
                        particles.Text(p.pos - new Vector2(0f, 18f), msg, pk.Color, 20);
                    }
                }

                if (!pk.alive) pickups.RemoveAt(i);
            }
        }

        // ---- waves ---------------------------------------------------------
        void UpdateWaves(float dt)
        {
            if (state == GameState.Break)
            {
                breakTimer -= dt;
                if (breakTimer <= 0f) StartWave();
                return;
            }

            if (_spawnQueue.Count > 0)
            {
                _spawnTimer -= dt;
                if (_spawnTimer <= 0f && zombies.Count < S.MAX_ALIVE)
                {
                    int burst = 1 + Mathf.Min(4, wave / 3);
                    for (int i = 0; i < burst && _spawnQueue.Count > 0; i++)
                    {
                        string key = _spawnQueue[_spawnQueue.Count - 1];
                        _spawnQueue.RemoveAt(_spawnQueue.Count - 1);
                        Spawn(key);
                    }
                    _spawnTimer = Mathf.Max(0.18f, (0.85f - wave * 0.04f) * mode.spawnMult);
                }
            }
            else if (zombies.Count == 0 || mode.continuous)
            {
                EndWave();
            }
        }

        void StartWave()
        {
            wave++;
            state = GameState.Playing;
            _spawnQueue.Clear();
            _spawnQueue.AddRange(ZombieTypes.BuildWave(wave, rng, mode.budgetMult, mode.bossEvery));
            _spawnTimer = 0f;

            _banner = "WAVE " + wave;
            bool bossNow = mode.bossEvery > 0 && wave % mode.bossEvery == 0;
            _bannerSub = bossNow ? "ABOMINATION INCOMING" : _spawnQueue.Count + " hostiles";
            _bannerT = 2.2f;
            audio.Play("wave", 0.8f);
        }

        void EndWave()
        {
            state = GameState.Break;
            breakTimer = mode.waveBreak;
            if (mode.healOnWave > 0) player.Heal(mode.healOnWave);

            int bonus = (int)((60 + wave * 25) * mode.scoreMult);
            score += bonus;
            _banner = "WAVE " + wave + " CLEARED";
            _bannerSub = "+" + bonus + " bonus";
            _bannerT = 2.4f;
            audio.Play("wave", 0.5f);

            // endless modes roll waves every few seconds, do not carpet the floor
            if (!mode.continuous || wave % 3 == 0) SupplyDrop();

            if (wave % 5 == 0)
            {
                var c = new Pickup(world.FreePoint(rng, player.pos, 90f, 260f), "case", null, -1f);
                c.caseKey = Skins.DropCaseKey(rng, wave);
                pickups.Add(c);
            }
        }

        void SupplyDrop()
        {
            Player p = player;
            var spots = new List<Vector2>();
            for (int i = 0; i < 12; i++)
            {
                float ang = rng.Range(0f, 360f);
                Vector2 pos = p.pos + MathUtil.FromAngle(ang, rng.Range(90f, 220f));
                pos.x = MathUtil.Clamp(pos.x, S.WALL + 30, S.WORLD_W - S.WALL - 30);
                pos.y = MathUtil.Clamp(pos.y, S.WALL + 30, S.WORLD_H - S.WALL - 30);
                if (!world.Blocked(pos, 20f)) spots.Add(pos);
            }
            if (spots.Count == 0) spots.Add(p.pos);

            pickups.Add(new Pickup(rng.Pick(spots), "ammo", null, -1f));

            if (p.hp < p.maxHp * 0.75f || wave % 2 == 0)
                pickups.Add(new Pickup(rng.Pick(spots), "medkit", null, -1f));

            if (wave % 3 == 0)
            {
                var missing = new List<string>();
                for (int i = 1; i < Weapons.Order.Length; i++)
                    if (!p.Owns(Weapons.Order[i])) missing.Add(Weapons.Order[i]);

                string key = missing.Count > 0
                    ? rng.Pick(missing)
                    : Weapons.Order[1 + (int)(rng.NextUInt() % 3u)];
                pickups.Add(new Pickup(rng.Pick(spots), "weapon", key, -1f));
            }
        }

        void Spawn(string key)
        {
            float hp, sp, dmg;
            ZombieTypes.WaveScales(wave, out hp, out sp, out dmg);
            hp *= mode.hpMult;
            sp *= mode.speedMult;
            dmg *= mode.dmgMult;

            ZombieSpec spec = ZombieTypes.Get(key);
            Vector2 pos = world.PickSpawn(player.pos, rng);
            if (key == "boss") hp *= 1f + (wave / 5 - 1) * 0.45f;

            var z = new Zombie(spec, pos, rng, hp, sp, dmg);
            world.Collide(ref z.pos, z.radius);
            zombies.Add(z);
        }

        // ---- death ---------------------------------------------------------
        void EndRun()
        {
            state = GameState.GameOver;
            audio.Play("gameover", 0.9f);
            particles.Gore(player.pos, 40);
            Shake(16f);

            _runScrap = Mathf.Max(1, score / 60);
            inventory.scrap += _runScrap;

            save.bestScore = Mathf.Max(save.bestScore, score);
            save.bestWave = Mathf.Max(save.bestWave, wave);
            save.bestKills = Mathf.Max(save.bestKills, player.kills);
            save.Submit(save.modes, mode.key, score, wave, player.kills);
            save.Submit(save.maps, mapdef.key, score, wave, player.kills);
            Persist();
        }

        // -------------------------------------------------------------- draw
        public void Render()
        {
            Painter.GlobalAlpha = 1f;
            Painter.Begin();

            switch (state)
            {
                case GameState.Splash: _splash.Draw(); break;
                case GameState.Loading: _loader.Draw(); break;
                case GameState.Menu: menu.Draw(); break;
                default: RenderGame(); break;
            }

            Painter.End();
        }

        void RenderGame()
        {
            Vector2 cam = _cameraPos;
            if (_shakeMag > 0.3f)
                cam += new Vector2(rng.Range(-_shakeMag, _shakeMag), rng.Range(-_shakeMag, _shakeMag));
            _drawCam = cam;

            world.Draw(cam);

            for (int i = 0; i < pickups.Count; i++) pickups[i].Draw(cam);
            for (int i = 0; i < zombies.Count; i++) DrawZombieLit(zombies[i], cam);
            player.Draw(cam);
            for (int i = 0; i < enemyBullets.Count; i++) enemyBullets[i].Draw(cam);
            for (int i = 0; i < bullets.Count; i++) bullets[i].Draw(cam);
            particles.Draw(cam);

            DrawLighting(cam);
            particles.DrawText(cam);

            if (_hurtFlash > 0f)
                Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT,
                             new Color(170f / 255f, 20f / 255f, 20f / 255f,
                                       90f / 255f * Mathf.Min(1f, _hurtFlash)));

            float low = player.hp / player.maxHp;
            if (low < 0.3f && player.alive)
            {
                float pulse = (Mathf.Sin(_menuT * 6f) + 1f) * 0.5f;
                Painter.RectOutline(0f, 0f, S.WIDTH, S.HEIGHT, 26f,
                                    new Color(150f / 255f, 16f / 255f, 16f / 255f,
                                              (40f + 55f * pulse) / 255f));
            }

            Hud.Draw(this);

            if (_bannerT > 0f)
                Hud.DrawBanner(_banner, _bannerSub, Mathf.Min(1f, _bannerT / 0.6f));

            if (state == GameState.Playing || state == GameState.Break)
                Hud.DrawCrosshair(_mouse, player);

            if (state == GameState.Paused) DrawPause();
            if (state == GameState.GameOver) DrawGameOver();
        }

        // ---- what the torch actually reveals -------------------------------
        const float TORCH_RANGE = 760f;
        static readonly float TORCH_COS = Mathf.Cos(30f * Mathf.Deg2Rad);
        static readonly float TORCH_COS_IN = Mathf.Cos(22f * Mathf.Deg2Rad);
        const float BUBBLE = 150f;
        const float BITE = 62f;

        /// <summary>0 = invisible in the dark, 1 = fully lit. Matches the light overlay.</summary>
        public float Visibility(Vector2 pos)
        {
            Player p = player;
            Vector2 d = pos - p.pos;
            float dist = d.magnitude;

            if (dist <= BITE) return 1f;
            if (dist <= BUBBLE)
                return MathUtil.Clamp(1f - (dist - BITE) / (BUBBLE - BITE) * 0.45f, 0.55f, 1f);
            if (_muzzleFlash > 0.05f && dist < 420f) return 1f;
            if (!p.flashlightOn) return 0f;
            if (dist > TORCH_RANGE) return 0f;

            Vector2 dirn = d / Mathf.Max(dist, 0.001f);
            float cosa = dirn.x * p.aim.x + dirn.y * p.aim.y;
            if (cosa < TORCH_COS) return 0f;
            if (!world.HasLos(p.pos, pos)) return 0f;

            float edge = MathUtil.Clamp((cosa - TORCH_COS) /
                                        Mathf.Max(1e-5f, TORCH_COS_IN - TORCH_COS), 0f, 1f);
            float falloff = MathUtil.Clamp(1f - (dist - BUBBLE) / (TORCH_RANGE - BUBBLE), 0f, 1f);
            return MathUtil.Clamp(edge * (0.35f + 0.65f * falloff), 0f, 1f);
        }

        void DrawZombieLit(Zombie z, Vector2 cam)
        {
            float v = Visibility(z.pos);
            if (v <= 0.03f) return;

            if (v >= 0.93f) { z.Draw(cam); return; }

            Painter.GlobalAlpha = v;
            z.Draw(cam);
            Painter.GlobalAlpha = 1f;
        }

        public float DarknessLevel { get { return Mathf.Max(150f, S.DARKNESS - mapdef.ambient); } }

        void DrawLighting(Vector2 cam)
        {
            Lighting.Begin();

            Vector2 pscreen = player.pos - cam;
            Lighting.AddPoint(pscreen, S.LIGHT_RADIUS, 1.5f);

            if (_muzzleFlash > 0.05f)
            {
                Vector2 mp = player.MuzzlePos() - cam;
                Lighting.AddPoint(mp, 260f * _muzzleFlash, 1.1f);
            }

            for (int i = 0; i < pickups.Count; i++)
                Lighting.AddPoint(pickups[i].pos - cam, 70f, 1.2f);

            Lighting.Draw(DarknessLevel, pscreen, player.aim, player.flashlightOn);

            if (_muzzleFlash > 0.05f)
            {
                Vector2 mp = player.MuzzlePos() - cam;
                Painter.Flip(true);
                Painter.RadialBlob(mp.x, mp.y, 150f,
                                   new Color(1f, 0.92f, 0.72f, 120f / 255f * _muzzleFlash));
                Painter.Flip(false);
            }
        }

        // ---- screens -------------------------------------------------------
        void DrawPause()
        {
            Txt.DropPending();
            Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, new Color(6f / 255f, 7f / 255f, 9f / 255f, 180f / 255f));

            Txt.Draw("PAUSED", 76, S.WIDTH / 2f, S.HEIGHT / 2f - 90f, S.C_WHITE, Anchor.MidTop, true);
            Txt.Draw("Esc / P  -  resume", 24, S.WIDTH / 2f, S.HEIGHT / 2f + 10f, S.C_DIM, Anchor.MidTop);
            Txt.Draw("R  -  restart        Q  -  main menu", 24, S.WIDTH / 2f,
                     S.HEIGHT / 2f + 46f, S.C_DIM, Anchor.MidTop);

            Boot.DrawMark(S.WIDTH / 2f, S.HEIGHT - 86f, 0.22f, 1f);
            Txt.Draw(S.STUDIO, 14, S.WIDTH / 2f, S.HEIGHT - 48f,
                     S.Col(120, 124, 134), Anchor.MidTop, true);
        }

        void DrawGameOver()
        {
            Txt.DropPending();
            Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT,
                         new Color(40f / 255f, 4f / 255f, 4f / 255f, 190f / 255f));

            Player p = player;
            float acc = p.shots > 0 ? p.hits / (float)p.shots * 100f : 0f;

            Txt.Draw("YOU DIED", 96, S.WIDTH / 2f, 120f, S.C_RED, Anchor.MidTop, true);

            string[] keys = { "WAVE REACHED", "KILLS", "SCORE", "ACCURACY", "TIME", "SCRAP EARNED" };
            string[] vals =
            {
                wave.ToString(), p.kills.ToString(), score.ToString(),
                acc.ToString("0") + "%",
                ((int)(timeAlive / 60f)) + ":" + ((int)(timeAlive % 60f)).ToString("00"),
                _runScrap.ToString()
            };

            float y = 262f;
            for (int i = 0; i < keys.Length; i++)
            {
                Txt.Draw(keys[i], 24, S.WIDTH / 2f - 30f, y, S.C_DIM, Anchor.TopRight);
                Txt.Draw(vals[i], 24, S.WIDTH / 2f + 30f, y, S.C_WHITE, Anchor.TopLeft, true);
                y += 40f;
            }

            if (score >= save.bestScore && score > 0)
                Txt.Draw("NEW RECORD", 30, S.WIDTH / 2f, y + 10f, S.C_YELLOW, Anchor.MidTop, true);
            else
                Txt.Draw("best " + save.bestScore, 22, S.WIDTH / 2f, y + 12f, S.C_DIM, Anchor.MidTop);

            Txt.Draw("R / ENTER  -  play again        ESC  -  menu", 24, S.WIDTH / 2f,
                     S.HEIGHT - 70f, S.C_WHITE, Anchor.MidBottom);
            Txt.Draw(S.STUDIO, 14, S.WIDTH / 2f, S.HEIGHT - 26f,
                     S.Col(120, 124, 134), Anchor.MidBottom, true);
        }

        // ------------------------------------------------------------- imgui
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Letterbox.GuiMatrix();
            Txt.Flush();
            GUI.matrix = old;
        }

        void OnApplicationQuit() { Persist(); }
    }

    /// <summary>Drives the GL pass from the render camera.</summary>
    [DisallowMultipleComponent]
    public class PainterCamera : MonoBehaviour
    {
        public GameApp app;
        void OnPostRender() { if (app != null) app.Render(); }
    }
}
