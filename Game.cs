using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NLog;
using System.Collections.Generic;
using System.Linq;

namespace klotski
{
    enum Direction
    {
        UP,
        DOWN,
        LEFT,
        RIGHT
    }

    class GameObject
    {
        public GameObject(Rectangle rectangle, Texture2D image)
        {
            Rectangle = rectangle;
            Image = image;
        }

        public GameObject(Point location, Texture2D image)
        {
            Rectangle = new Rectangle(location.X, location.Y, image.Width, image.Height);
            Image = image;
        }

        public Rectangle Rectangle { get; set; }

        public Texture2D Image { get; }

        public void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(Image, Rectangle, Color.White);
        }

        public bool Contains(Point location)
        {
            return Rectangle.Contains(location);
        }

        public void Move(Point shift)
        {
            Rectangle.Offset(shift);
        }
    }

    class Block : GameObject
    {
        public Block(Point location, Texture2D image) : base(location, image) { }
    }

    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game : Microsoft.Xna.Framework.Game
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        const int WINDOW_HEIGHT = 224;
        const int WINDOW_WIDTH = 184;
        const int BOARD_OFFSET_X = 12;
        const int BOARD_OFFSET_Y = 12;
        const int MIN_BLOCK_WIDTH = 40;
        const int MIN_BLOCK_HEIGHT = 40;

        static readonly int[][] blocksInfo = new int[][] {
            new int[]{ 0, 0, 1, 2 },
            new int[]{ 1, 0, 2, 2 },
            new int[]{ 3, 0, 1, 2 },
            new int[]{ 0, 2, 1, 2 },
            new int[]{ 1, 2, 2, 1 },
            new int[]{ 3, 2, 1, 2 },
            new int[]{ 1, 3, 1, 1 },
            new int[]{ 2, 3, 1, 1 },
            new int[]{ 0, 4, 1, 1 },
            new int[]{ 3, 4, 1, 1 }
        };

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        List<GameObject> objects;
        Rectangle boardRect;

        bool buttonPressed;
        Point clickPosition;
        GameObject selected;

        public Game()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            IsMouseVisible = true;
            
            graphics.PreferredBackBufferHeight = WINDOW_HEIGHT;
            graphics.PreferredBackBufferWidth = WINDOW_WIDTH;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            logger.Trace("Initialize");
            base.Initialize();
        }

        private static int BlockSizeToImageIdx(int width, int height)
        {
            return (width - 1) * 2 + (height - 1);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            logger.Trace("LoadContent");

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            objects = new List<GameObject>();

            var blocksImages = new Texture2D[] {
                Content.Load<Texture2D>("1x1"), //0
                Content.Load<Texture2D>("1x2"), //1
                Content.Load<Texture2D>("2x1"), //2
                Content.Load<Texture2D>("2x2"), //3
            };

            objects.Add(new GameObject(new Point(0, 0), Content.Load<Texture2D>("background")));

            var frame = new GameObject(new Point(0, 0), Content.Load<Texture2D>("frame"));
            objects.Add(frame);
            {
                var r = frame.Rectangle;
                boardRect = new Rectangle(r.X + BOARD_OFFSET_X, r.Y + BOARD_OFFSET_Y, r.Width - BOARD_OFFSET_X * 2, r.Height - BOARD_OFFSET_Y * 2);
            }

            foreach (var blockInfo in blocksInfo)
            {
                int x = blockInfo[0];
                int y = blockInfo[1];

                int width = blockInfo[2];
                int height = blockInfo[3];

                var image = blocksImages[BlockSizeToImageIdx(width, height)];
                objects.Add(new Block(new Point(x * MIN_BLOCK_WIDTH + BOARD_OFFSET_X, y * MIN_BLOCK_HEIGHT + BOARD_OFFSET_Y), image));
            }
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // For Mobile devices, this logic will close the Game when the Back button is pressed
            // Exit() is obsolete on iOS
#if !__IOS__ && !__TVOS__
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
#endif

            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                if (!buttonPressed)
                {
                    OnMousePress(Mouse.GetState().Position);
                    buttonPressed = true;
                }
                else
                {
                    OnMouseHold(Mouse.GetState().Position);
                }
            }
            else if (buttonPressed)
            {
                buttonPressed = false;
                OnMouseRelease(Mouse.GetState().Position);
            }

            base.Update(gameTime);
        }

        private void OnMousePress(Point position)
        {
            logger.Debug($"OnMousePress {position}");

            clickPosition = position;

            selected = objects.FirstOrDefault((obj) => obj is Block && obj.Contains(position));
        }

        private void OnMouseHold(Point position)
        {
            if (selected != null)
            {
                var delta = position - clickPosition;

                int dX = delta.X / (MIN_BLOCK_HEIGHT / 2);
                int dY = delta.Y / (MIN_BLOCK_WIDTH / 2);

                if ((dX != 0 && dY == 0) || (dX == 0 && dY != 0))
                {
                    logger.Debug($"OnMouseHold - moving {position}");

                    Direction direction;
                     
                    if (dX > 0)
                    {
                        direction = Direction.RIGHT;
                    }
                    else if (dX < 0)
                    {
                        direction = Direction.LEFT;
                    }
                    else if (dY > 0)
                    {
                        direction = Direction.DOWN;
                    }
                    else //if (dY < 0)
                    {
                        direction = Direction.UP;
                    }

                    bool success = TryMoveBlock(direction);

                    if (success)
                    {
                        clickPosition = position;
                    }
                }
            }
        }

        private bool TryMoveBlock(Direction direction)
        {
            logger.Debug($"TryMoveBlock {direction}");

            Point offset;
            switch (direction)
            {
                case Direction.LEFT:
                    offset = new Point(-MIN_BLOCK_WIDTH, 0);
                    break;
                case Direction.RIGHT:
                    offset = new Point(MIN_BLOCK_WIDTH, 0);
                    break;
                case Direction.UP:
                    offset = new Point(0, -MIN_BLOCK_HEIGHT);
                    break;
                case Direction.DOWN:
                    offset = new Point(0, MIN_BLOCK_HEIGHT);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var newRectangle = selected.Rectangle;
            newRectangle.Offset(offset);

            if (newRectangle.Left < boardRect.Left || newRectangle.Top < boardRect.Top
                || newRectangle.Right > boardRect.Right || newRectangle.Bottom > boardRect.Bottom)
            {
                // The block is out of the board
                return false;
            }

            if (objects.Any((obj) => 
                    obj is Block && obj != selected && obj.Rectangle.Intersects(newRectangle)))
            {
                return false;
            }

            logger.Debug($"success!");

            selected.Rectangle = newRectangle;

            return true;
        }

        private void OnMouseRelease(Point position)
        {
            if (selected != null)
            {
                logger.Debug($"OnMouseRelease {position}");
                selected = null;
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();

            foreach (var obj in objects)
            {
                obj.Draw(spriteBatch);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}

