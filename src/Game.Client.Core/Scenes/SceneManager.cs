using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Core.Scenes;

public abstract class Scene
{
    protected SceneManager SceneManager { get; private set; } = null!;
    protected NetworkClient NetworkClient { get; private set; } = null!;
    protected ContentManager Content { get; private set; } = null!;

    internal void Initialize(SceneManager sceneManager, NetworkClient networkClient, ContentManager content)
    {
        SceneManager = sceneManager;
        NetworkClient = networkClient;
        Content = content;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void Update(GameTime gameTime);
    public abstract void Draw(SpriteBatch spriteBatch, GameTime gameTime);
}

public class SceneManager
{
    private readonly Microsoft.Xna.Framework.Game _game;
    private readonly NetworkClient _networkClient;
    private ContentManager _content = null!;
    private Scene? _currentScene;

    public SceneManager(Microsoft.Xna.Framework.Game game, NetworkClient networkClient)
    {
        _game = game;
        _networkClient = networkClient;
    }

    public void LoadContent(ContentManager content)
    {
        _content = content;
    }

    public void SetScene(Scene scene)
    {
        _currentScene?.Exit();
        _currentScene = scene;
        _currentScene.Initialize(this, _networkClient, _content);
        _currentScene.Enter();
    }

    public void Update(GameTime gameTime)
    {
        _currentScene?.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        _currentScene?.Draw(spriteBatch, gameTime);
    }
}
