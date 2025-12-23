using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Game.Client.Core.Scenes;

namespace Game.Client.Core;

public class GameMain : Microsoft.Xna.Framework.Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SceneManager _sceneManager = null!;
    private NetworkClient _networkClient = null!;
    private KeyboardState _prevKeyboard;

    // Shared rendering resources
    public static Texture2D? PixelTexture { get; private set; }
    public static SpriteFont? DefaultFont { get; private set; }

    // Lighting system
    public static RenderTarget2D? SceneTarget { get; private set; }
    public static RenderTarget2D? LightMapTarget { get; private set; }
    public static RenderTarget2D? ShadowTarget { get; private set; }
    public static Effect? LightingEffect { get; private set; }
    public static Texture2D? LightTexture { get; private set; }

    public GameMain()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void Initialize()
    {
        _networkClient = new NetworkClient();
        _sceneManager = new SceneManager(this, _networkClient);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a 1x1 white pixel texture for drawing shapes
        PixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        PixelTexture.SetData(new[] { Color.White });

        // Try to load font, fall back to null if not available
        try
        {
            DefaultFont = Content.Load<SpriteFont>("Fonts/Default");
        }
        catch
        {
            Console.WriteLine("[Client] No font loaded - text will not be displayed");
            DefaultFont = null;
        }

        // Create render targets for lighting
        SceneTarget = new RenderTarget2D(GraphicsDevice, 1280, 720);
        LightMapTarget = new RenderTarget2D(GraphicsDevice, 1280, 720);
        ShadowTarget = new RenderTarget2D(GraphicsDevice, 1280, 720);

        // Try to load lighting shader
        try
        {
            LightingEffect = Content.Load<Effect>("Shaders/Lighting");
            Console.WriteLine("[Client] Lighting shader loaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Client] Could not load lighting shader: {ex.Message}");
            LightingEffect = null;
        }

        // Create soft light texture (radial gradient)
        CreateLightTexture();

        _sceneManager.LoadContent(Content);
        _sceneManager.SetScene(new ConnectScene());
    }

    private void CreateLightTexture()
    {
        const int size = 256;
        LightTexture = new Texture2D(GraphicsDevice, size, size);
        var data = new Color[size * size];

        var center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var dist = MathF.Sqrt(dx * dx + dy * dy) / center;

                // Soft falloff using smoothstep-like curve
                var intensity = 1f - MathHelper.Clamp(dist, 0f, 1f);
                intensity = intensity * intensity * (3f - 2f * intensity); // Smoothstep

                var alpha = (byte)(intensity * 255);
                data[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        }

        LightTexture.SetData(data);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        // F11 to toggle fullscreen
        if (keyboard.IsKeyDown(Keys.F11) && _prevKeyboard.IsKeyUp(Keys.F11))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
        }

        _prevKeyboard = keyboard;

        _networkClient.PollEvents();
        _sceneManager.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 30, 40)); // Dark background
        _sceneManager.Draw(_spriteBatch, gameTime);
        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _networkClient.Dispose();
        base.OnExiting(sender, args);
    }
}
