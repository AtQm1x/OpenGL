using OpenGL.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace OpenGL
{
    // Be warned, there is a LOT of stuff here. It might seem complicated, but just take it slow and you'll be fine.
    // OpenGL's initial hurdle is quite large, but once you get past that, things will start making more sense.
    public class Window : GameWindow
    {
        struct tri
        {
            public Vector3[] p;

            public tri(Vector3 p1, Vector3 p2, Vector3 p3)
            {
                p = new Vector3[3];
                p[0] = p1;
                p[1] = p2;
                p[2] = p3;
            }
        } // triangle structure
        struct Mesh
        {
            public List<tri> Tris;

            public bool LoadFromObjectFile(string sFilename)
            {
                using (StreamReader file = new StreamReader(sFilename))
                {
                    Tris = new List<tri>();
                    if (file == null)
                        return false;

                    // Local cache of verts
                    List<Vector3> verts = new List<Vector3>();

                    while (!file.EndOfStream)
                    {
                        string line = file.ReadLine();
                        line = line.Replace('.', ',');
                        string[] tokens = line.Split(' ');

                        if (tokens.Length < 1)
                            continue;

                        char firstChar = tokens[0][0];

                        if (firstChar == 'v')
                        {
                            float x, y, z;
                            if (float.TryParse(tokens[1], out x) && float.TryParse(tokens[2], out y) && float.TryParse(tokens[3], out z))
                            {
                                verts.Add(new Vector3(x, y, z));
                            }
                            else
                            {
                                tokens[1] = tokens[1].Replace('.', ',');
                                tokens[2] = tokens[2].Replace('.', ',');
                                tokens[3] = tokens[3].Replace('.', ',');
                                float.TryParse(tokens[1], out x);
                                float.TryParse(tokens[2], out y);
                                float.TryParse(tokens[3], out z);
                                verts.Add(new Vector3(x, y, z));
                            }
                        }

                        if (firstChar == 'f')
                        {
                            int[] f = new int[3];
                            if (int.TryParse(tokens[1], out f[0]) && int.TryParse(tokens[2], out f[1]) && int.TryParse(tokens[3], out f[2]))
                            {
                                Tris.Add(new tri(verts[f[0] - 1], verts[f[1] - 1], verts[f[2] - 1]));
                            }
                        }
                    }
                }
                return true;
            }
        }
        // Create the vertices for our triangle. These are listed in normalized device coordinates (NDC)
        // In NDC, (0, 0) is the center of the screen.
        // Negative X coordinates move to the left, positive X move to the right.
        // Negative Y coordinates move to the bottom, positive Y move to the top.
        // OpenGL only supports rendering in 3D, so to create a flat triangle, the Z coordinate will be kept as 0.
        public float[] _vertices =
        {
            -0.5f, -0.5f, 0.0f, // Bottom-left vertex
             0.5f, -0.5f, 0.0f, // Bottom-right vertex
             0.0f,  0.5f, 0.0f  // Top vertex
        };
        Random r = new Random();

        public Matrix4 rotMatX, rotMatY, rotMatZ, projMat;

        // These are the handles to OpenGL objects. A handle is an integer representing where the object lives on the
        // graphics card. Consider them sort of like a pointer; we can't do anything with them directly, but we can
        // send them to OpenGL functions that need them.

        // What these objects are will be explained in OnLoad.
        private int _vertexBufferObject;

        private int _vertexArrayObject;

        private float aspectf = 1;

        private double FPS;
        private double UPS;

        private List<float[]> _triangleVerticesList = new List<float[]>();
        private List<float[]> _triangleDrawVerticesList = new List<float[]>();

        // This class is a wrapper around a shader, which helps us manage it.
        // The shader class's code is in the Common project.
        // What shaders are and what they're used for will be explained later in this tutorial.
        private Shader _shader;
        private Shader _lineshader;

        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            VSync = VSyncMode.On;
        }
        Mesh mesh;
        // Now, we start initializing OpenGL.
        protected override void OnLoad()
        {
            base.OnLoad();

            rotMatX = new Matrix4
            {
                M11 = 1f,
                M22 = (float)Math.Cos(tickdeg),
                M23 = -(float)Math.Sin(tickdeg),
                M32 = (float)Math.Sin(tickdeg),
                M33 = (float)Math.Cos(tickdeg)
            };
            rotMatY = new Matrix4
            {
                M11 = (float)Math.Cos(tickdeg),
                M13 = (float)Math.Sin(tickdeg),
                M22 = 1f,
                M31 = (float)-Math.Sin(tickdeg),
                M33 = (float)Math.Cos(tickdeg)
            };
            rotMatZ = new Matrix4
            {
                M11 = (float)Math.Cos(tickdeg),
                M12 = -(float)Math.Sin(tickdeg),
                M21 = (float)Math.Sin(tickdeg),
                M22 = (float)Math.Cos(tickdeg),
                M33 = 1.0f
            };
            projMat = new Matrix4
            {
                M11 = 1f,
                M22 = 1f,
                M44 = 1f
            };

            aspectf = (float)Size.X / (float)Size.Y;
            mesh.LoadFromObjectFile("Cube.obj");

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            _vertexBufferObject = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _vertexArrayObject = GL.GenVertexArray();

            GL.BindVertexArray(_vertexArrayObject);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // Enable variable 0 in the shader.
            GL.EnableVertexAttribArray(0);

            GL.LineWidth(3);

            _shader = new Shader("shaders/shader.vert", "shaders/shader.frag");
            _lineshader = new Shader("shaders/_lineShader.vert", "shaders/_lineShader.frag");

            // Now, enable the shader.
            // Just like the VBO, this is global, so every function that uses a shader will modify this one until a new one is bound instead.
            _shader.Use();

            // Setup is now complete! Now we move to the OnRenderFrame function to finally draw the triangle.
        }
        int tick = 0;
        double tickdeg = 0;
        // Now that initialization is done, let's create our render loop.
        float[] triangle = { 0, 0.5f, -0.5f, 0, 0.5f, 0 };
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            // Clear the color buffer
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // Use the shader program for filled triangles
            _shader.Use();
            foreach (tri t in mesh.Tris)
            {
                Vector3 p1 = t.p[0] / 1000000 / 2;
                Vector3 p2 = t.p[1] / 1000000 / 2;
                Vector3 p3 = t.p[2] / 1000000 / 2;

                p1 = Vector3.TransformPosition(p1, rotMatX);
                p2 = Vector3.TransformPosition(p2, rotMatX);
                p3 = Vector3.TransformPosition(p3, rotMatX);

                p1 = Vector3.TransformPosition(p1, rotMatY);
                p2 = Vector3.TransformPosition(p2, rotMatY);
                p3 = Vector3.TransformPosition(p3, rotMatY);

                p1 = Vector3.TransformPosition(p1, rotMatZ);
                p2 = Vector3.TransformPosition(p2, rotMatZ);
                p3 = Vector3.TransformPosition(p3, rotMatZ);

                p1 = Vector3.TransformPosition(p1, projMat);
                p2 = Vector3.TransformPosition(p2, projMat);
                p3 = Vector3.TransformPosition(p3, projMat);

                float[] t2 = { p1.X / aspectf, p1.Y, p1.Z, p2.X / aspectf, p2.Y, p2.Z, p3.X / aspectf, p3.Y, p3.Z };
                Vector3 normal, line1, line2;

                line1.X = t2[0] - t2[3];
                line1.Y = t2[1] - t2[4];
                line1.Z = t2[2] - t2[5];

                line2.X = t2[6] - t2[3];
                line2.Y = t2[7] - t2[4];
                line2.Z = t2[8] - t2[5];

                normal.X = line1.Y * line2.Z - line1.Z * line2.Y;
                normal.Y = line1.Z * line2.X - line1.X * line2.Z;
                normal.Z = line1.X * line2.Y - line1.Y * line2.X;

                float l = (float)Math.Sqrt((double)(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z));
                normal.X /= l;
                normal.Y /= l;
                normal.Z /= l;

                if (normal.Z > 0)
                {
                    addFilledTriangle(t2);
                    addDrawTriangle(t2);
                }
            }

            fillTriangles();

            _lineshader.Use();

            drawTriangles();

            _triangleVerticesList.Clear();
            _triangleDrawVerticesList.Clear();

            FPS = 1.0 / e.Time;
            // Update window title with FPS
            Title = $"OpenGL Window - FPS: {Math.Round(FPS, 1)}";

            // Swap the back buffer to the front
            SwapBuffers();
        }

        private void fillTriangles()
        {
            // Bind the buffer containing the vertex data
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            // Use the shader program
            _shader.Use();

            // Bind the Vertex Array Object before drawing
            GL.BindVertexArray(_vertexArrayObject);

            foreach (var triangleVertices in _triangleVerticesList)
            {
                // Update the buffer data with the current triangle vertices
                UpdateBufferData(triangleVertices);

                // Draw the filled triangle
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }
        }

        private void drawTriangles()
        {
            foreach (var triangleVertices in _triangleDrawVerticesList)
            {
                // Update the buffer data with the current triangle vertices
                UpdateBufferData(triangleVertices);

                // Draw the perimeter of the triangle using LineLoop
                GL.DrawArrays(PrimitiveType.LineLoop, 0, 3);
            }
        }

        private void UpdateBufferData(float[] triangleVertices)
        {
            // Ensure the input array has at least 6 elements
            if (triangleVertices.Length < 6)
                throw new ArgumentException("Input array must contain at least 6 elements.");

            // Update the buffer data with the provided vertices
            _vertices[0] = triangleVertices[0];
            _vertices[1] = triangleVertices[1];
            _vertices[3] = triangleVertices[3];
            _vertices[4] = triangleVertices[4];
            _vertices[6] = triangleVertices[6];
            _vertices[7] = triangleVertices[7];

            // Update only the parts of the buffer that have changed
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _vertices.Length * sizeof(float), _vertices);
        }

        // To add a new triangle to be drawn
        private void addFilledTriangle(float[] triangleVertices)
        {
            _triangleVerticesList.Add(triangleVertices);
        }

        private void addDrawTriangle(float[] triangleVertices)
        {
            _triangleDrawVerticesList.Add(triangleVertices);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            tick++;
            tickdeg = tick / 1 * Math.PI / 180;

            rotMatX = new Matrix4
            {
                M11 = 1f,
                M22 = (float)Math.Cos(tickdeg),
                M23 = -(float)Math.Sin(tickdeg),
                M32 = (float)Math.Sin(tickdeg),
                M33 = (float)Math.Cos(tickdeg)
            };
            rotMatY = new Matrix4
            {
                M11 = (float)Math.Cos(tickdeg * 1.01),
                M13 = (float)Math.Sin(tickdeg * 1.01),
                M22 = 1f,
                M31 = (float)-Math.Sin(tickdeg * 1.01),
                M33 = (float)Math.Cos(tickdeg * 1.01)
            };
            rotMatZ = new Matrix4
            {
                M11 = (float)Math.Cos(tickdeg * 1.0),
                M12 = -(float)Math.Sin(tickdeg * 1.0),
                M21 = (float)Math.Sin(tickdeg * 1.0),
                M22 = (float)Math.Cos(tickdeg * 1.0),
                M33 = 1.0f
            };
            projMat = new Matrix4
            {
                M11 = 1f,
                M22 = 1f,
                M44 = 1f
            };

            var input = KeyboardState;
            if (input.IsKeyDown(Keys.Escape))
            {
                Close();
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // When the window gets resized, we have to call GL.Viewport to resize OpenGL's viewport to match the new size.
            // If we don't, the NDC will no longer be correct.
            aspectf = (float)Size.X / (float)Size.Y;
            Console.WriteLine(aspectf);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        // Now, for cleanup.
        // You should generally not do cleanup of opengl resources when exiting an application,
        // as that is handled by the driver and operating system when the application exits.
        // 
        // There are reasons to delete opengl resources, but exiting the application is not one of them.
        // This is provided here as a reference on how resource cleanup is done in opengl, but
        // should not be done when exiting the application.
        //
        // Places where cleanup is appropriate would be: to delete textures that are no
        // longer used for whatever reason (e.g. a new scene is loaded that doesn't use a texture).
        // This would free up video ram (VRAM) that can be used for new textures.
        //
        // The coming chapters will not have this code.
        protected override void OnUnload()
        {
            // Unbind all the resources by binding the targets to 0/null.
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete all the resources.
            GL.DeleteBuffer(_vertexBufferObject);
            GL.DeleteVertexArray(_vertexArrayObject);

            GL.DeleteProgram(_shader.Handle);

            base.OnUnload();
        }
    }
}
