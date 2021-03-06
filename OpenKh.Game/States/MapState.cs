using Microsoft.Xna.Framework.Graphics;
using OpenKh.Common;
using OpenKh.Engine.MonoGame;
using OpenKh.Game.Debugging;
using OpenKh.Game.Field;
using OpenKh.Game.Infrastructure;
using System.Numerics;
using xna = Microsoft.Xna.Framework;

namespace OpenKh.Game.States
{
    public class MapState : IState, IGameContext, IDebugConsumer
    {
        private readonly static BlendState AlphaBlendState = new BlendState()
        {
            ColorSourceBlend = Blend.SourceAlpha,
            AlphaSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaDestinationBlend = Blend.InverseSourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            AlphaBlendFunction = BlendFunction.Add,
            BlendFactor = xna.Color.White,
            MultiSampleMask = int.MaxValue,
            IndependentBlendEnable = false
        };

        public Kernel Kernel { get; private set; }
        private IDataContent _dataContent;
        private ArchiveManager _archiveManager;
        private xna.GraphicsDeviceManager _graphics;
        private InputManager _input;
        private IStateChange _stateChange;
        private KingdomShader _shader;
        private Camera _camera;

        public IField Field { get; private set; }

        private MenuState _menuState;

        public void Initialize(StateInitDesc initDesc)
        {
            Kernel = initDesc.Kernel;
            _dataContent = initDesc.DataContent;
            _archiveManager = initDesc.ArchiveManager;
            _graphics = initDesc.GraphicsDevice;
            _input = initDesc.InputManager;
            _stateChange = initDesc.StateChange;
            _shader = new KingdomShader(initDesc.ContentManager);
            _camera = new Camera()
            {
                CameraPosition = new Vector3(0, 100, 200),
                CameraRotationYawPitchRoll = new Vector3(90, 0, 10),
            };
            _menuState = new MenuState(this);

            Kernel.World = initDesc.StateSettings.GetInt("WorldId", Kernel.World);
            Kernel.Area = initDesc.StateSettings.GetInt("PlaceId", Kernel.Area);
            Kernel.Entrance = initDesc.StateSettings.GetInt("SpawnId", Kernel.Entrance);
            Field = new Kh2Field(
                Kernel,
                _camera,
                initDesc.StateSettings,
                _graphics.GraphicsDevice,
                _shader,
                _input);

            BasicallyForceToReloadEverything();
            _menuState.Initialize(initDesc);
        }

        public void Destroy()
        {
            _menuState.Destroy();
        }

        public void Update(DeltaTimes deltaTimes)
        {
            if (_menuState.IsMenuOpen)
            {
                _menuState.Update(deltaTimes);
                return;
            }

            if (_input.IsStart)
            {
                _menuState.OpenMenu();
            }
            else if (Kernel.DebugMode)
            {
                const double Speed = 100.0;
                var speed = (float)(deltaTimes.DeltaTime * Speed);

                if (_input.W)
                    _camera.CameraPosition += Vector3.Multiply(_camera.CameraLookAtX, speed * 5);
                if (_input.S)
                    _camera.CameraPosition -= Vector3.Multiply(_camera.CameraLookAtX, speed * 5);
                if (_input.A)
                    _camera.CameraPosition -= Vector3.Multiply(_camera.CameraLookAtY, speed * 5);
                if (_input.D)
                    _camera.CameraPosition += Vector3.Multiply(_camera.CameraLookAtY, speed * 5);

                if (_input.Up)
                    _camera.CameraRotationYawPitchRoll += new Vector3(0, 0, 1 * speed);
                if (_input.Down)
                    _camera.CameraRotationYawPitchRoll -= new Vector3(0, 0, 1 * speed);
                if (_input.Left)
                    _camera.CameraRotationYawPitchRoll += new Vector3(1 * speed, 0, 0);
                if (_input.Right)
                    _camera.CameraRotationYawPitchRoll -= new Vector3(1 * speed, 0, 0);

                Field.Update(deltaTimes.DeltaTime);
            }
        }

        public void Draw(DeltaTimes deltaTimes)
        {
            _camera.AspectRatio = _graphics.PreferredBackBufferWidth / (float)_graphics.PreferredBackBufferHeight;

            _graphics.GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;

            _shader.Pass(pass =>
            {
                _graphics.GraphicsDevice.BlendState = BlendState.Opaque;
                _shader.UseAlphaMask = true;

                DrawAllMeshes(pass, /*passRenderOpaque=*/true);

                _graphics.GraphicsDevice.BlendState = AlphaBlendState;
                _shader.UseAlphaMask = false;

                DrawAllMeshes(pass, /*passRenderOpaque=*/false);
            });
            Field.Draw();

            if (_menuState.IsMenuOpen)
            {
                _menuState.Draw(deltaTimes);
            }
        }

        private void DrawAllMeshes(EffectPass pass, bool passRenderOpaque)
        {
            _graphics.GraphicsDevice.DepthStencilState = passRenderOpaque ? DepthStencilState.Default : DepthStencilState.DepthRead;

            _shader.SetProjectionView(_camera.Projection);
            _shader.SetWorldView(_camera.World);
            _shader.SetModelViewIdentity();
            pass.Apply();

            Field.Render(_camera, _shader, pass, passRenderOpaque);
        }

        private void BasicallyForceToReloadEverything()
        {
            switch (Field)
            {
                case Kh2Field kh2Field:
                    kh2Field.LoadArea(Kernel.World, Kernel.Area);
                    break;
            }
        }

        public void LoadTitleScreen() => _stateChange.State = 0;

        public void LoadPlace(int world, int area, int entrance)
        {
            Kernel.World = world;
            Kernel.Area = area;
            Kernel.Entrance = entrance;

            BasicallyForceToReloadEverything();
        }

        public void DebugUpdate(IDebug debug)
        {
            if (_input.IsDebug)
                Kernel.DebugMode = !Kernel.DebugMode;
        }

        public void DebugDraw(IDebug debug)
        {
            if (_menuState.IsMenuOpen)
                return;

            if (Kernel.DebugMode)
            {
                debug.Println($"MAP: {Kh2.Constants.WorldIds[Kernel.World]}{Kernel.Area:D02}");
                debug.Println($"POS ({_camera.CameraPosition.X:F0}, {_camera.CameraPosition.Y:F0}, {_camera.CameraPosition.Z:F0})");
                debug.Println($"LKT ({_camera.CameraLookAt.X:F0}, {_camera.CameraLookAt.Y:F0}, {_camera.CameraLookAt.Z:F0})");
            }
        }
    }
}
