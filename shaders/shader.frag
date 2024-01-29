#version 330 core
out vec4 FragColor;
  
in vec3 pos; // the input variable from the vertex shader (same name and same type)  
uniform vec3 iResolution;
uniform vec4 col;
void main()
{
    //vec2 uv = pos / 
    FragColor = vec4(1.0, 1.0, 1.0, 1.0);
} 