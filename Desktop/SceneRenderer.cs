using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using U = UnityEngine;
using static Raylib_cs.Raylib;

namespace Carousel.Desktop;
internal sealed unsafe class SceneRenderer : IDisposable
{
    const int ShadowSize = 1024;
    readonly Dictionary<string, Mesh> primitives = new();
    static readonly Dictionary<U.Mesh, Mesh[]> sectors = new();
    readonly List<(Mesh mesh, Matrix4x4 transform, Color color, bool castsShadow)> draws = new();
    readonly List<U.TextMesh> text = new();
    readonly Material lit, shadow;
    readonly RenderTexture2D shadowMap;
    readonly Shader litShader, depthShader;
    readonly Camera3D lightCamera;
    readonly U.Light flash;
    readonly int flashUniform, eyeUniform;
    readonly Vector3 lightDir;
    static readonly Matrix4x4 reflectZ = Matrix4x4.CreateScale(1, 1, -1);
    static Vector3 World(U.Vector3 v) => new(v.x, v.y, -v.z);
    const string Vertex = """
        #version 330
        in vec3 vertexPosition;
        in vec3 vertexNormal;
        uniform mat4 mvp;
        uniform mat4 matModel;
        out vec3 pos;
        out vec3 norm;
        void main(){
            pos = (matModel*vec4(vertexPosition,1.0)).xyz;
            norm = mat3(transpose(inverse(matModel)))*vertexNormal;
            gl_Position=mvp*vec4(vertexPosition,1.0);
        }
        """;
    const string Depth = """
        #version 330
        in vec3 pos;
        uniform vec3 origin, forwardDir;
        out vec4 result;
        void main(){
            float d=clamp(dot(pos-origin,forwardDir)/40.0,0.0,0.99999)*255.0;
            result=vec4(floor(d)/255.0,fract(d),0.0,1.0);
        }
        """;
    const string Surface = """
        #version 330
        in vec3 pos;
        in vec3 norm;
        uniform vec4 colDiffuse, flash;
        uniform vec3 eyePosition;
        uniform vec3 origin,forwardDir,basisX,basisY;
        uniform sampler2D shadeMap;
        out vec4 result;
        void main(){
            vec3 p=pos-origin,n=normalize(norm);
            vec2 uv=vec2(dot(p,basisX),dot(p,basisY))/15.0+0.5;
            float facing=max(dot(n,-forwardDir),0.0);
            float d=dot(p,forwardDir)-0.014-0.025*(1.0-facing);
            float visibility=0.0;
            for(int i=-1;i<=1;i++) for(int j=-1;j<=1;j++){
                vec2 packedDepth=texture(shadeMap,uv+vec2(i,j)/1024.0).rg;
                visibility+=step(d,40.0*(packedDepth.x+packedDepth.y/255.0))/9.0;
            }
            if(any(lessThan(uv,vec2(0.0)))||any(greaterThan(uv,vec2(1.0)))) visibility=1.0;
            vec3 fill=vec3(.70,.71,.70)+.09*n.y;
            vec3 sunlight=vec3(.42,.37,.30)*facing*visibility;
            vec3 towardFlash = flash.xyz-pos;
            float r2=max(.01,dot(towardFlash,towardFlash));
            float glint = flash.w*max(0.0,dot(n,towardFlash*inversesqrt(r2)))/(1.0+2.5*r2);
            vec3 view=normalize(eyePosition-pos);
            float spec=pow(max(dot(n,normalize(view-forwardDir)),0.),30.)*visibility;
            float sky=pow(clamp(reflect(-view,n).y*.5+.5,0.,1.),4.);
            vec3 metalLight=colDiffuse.a*(.29*spec+.18*sky)*vec3(.88,.94,1.);
            result=vec4(colDiffuse.rgb*(fill+sunlight+glint*vec3(1,.7,.3))+metalLight,1);
        }
        """;
    public SceneRenderer()
    {
        primitives["Cube"] = GenMeshCube(1, 1, 1);
        primitives["Sphere"] = GenMeshSphere(.5f, 16, 24);
        var cylinder = GenMeshCylinder(.5f, 2, 32);
        var bounds = GetMeshBoundingBox(cylinder);
        float centerY = (bounds.Min.Y + bounds.Max.Y) * .5f;
        if (Math.Abs(centerY) > .0001f)
        {
            for (int i = 0; i < cylinder.VertexCount; i++)
                cylinder.Vertices[i * 3 + 1] -= centerY;
            UpdateMeshBuffer(cylinder, 0, cylinder.Vertices, cylinder.VertexCount * 3 * sizeof(float), 0);
        }

        primitives["Cylinder"] = cylinder;
        shadowMap = LoadRenderTexture(ShadowSize, ShadowSize);
        depthShader = LoadShaderFromMemory(Vertex, Depth);
        litShader = LoadShaderFromMemory(Vertex, Surface);
        lit = LoadMaterialDefault();
        lit.Shader = litShader;
        shadow = LoadMaterialDefault();
        shadow.Shader = depthShader;
        lit.Maps[(int)MaterialMapIndex.Metalness].Texture = shadowMap.Texture;
        litShader.Locs[(int)ShaderLocationIndex.MapMetalness] = GetShaderLocation(litShader, "shadeMap");
        var sun = U.GameObject.all.Select(o => o.GetComponent<U.Light>()).First(l => l != null && l.type == U.LightType.Directional);
        flash = U.GameObject.all.Select(o => o.GetComponent<U.Light>()).First(l => l != null && l.type == U.LightType.Point);
        flashUniform = GetShaderLocation(litShader, "flash");
        eyeUniform = GetShaderLocation(litShader, "eyePosition");
        lightDir = Vector3.Normalize(World(sun.transform.forward));
        Vector3 center = new(0, 1, 0), origin = center - lightDir * 16;
        lightCamera = new Camera3D(origin, center, Vector3.UnitY, 15, CameraProjection.Orthographic);
        foreach (var shader in new[]
        {
            litShader,
            depthShader
        }

        )
        {
            shader.Locs[(int)ShaderLocationIndex.MatrixModel] = GetShaderLocation(shader, "matModel");
            SetShaderValue(shader, GetShaderLocation(shader, "origin"), origin, ShaderUniformDataType.Vec3);
            SetShaderValue(shader, GetShaderLocation(shader, "forwardDir"), lightDir, ShaderUniformDataType.Vec3);
        }

        var right = Vector3.Normalize(Vector3.Cross(lightDir, Vector3.UnitY));
        SetShaderValue(litShader, GetShaderLocation(litShader, "basisX"), right, ShaderUniformDataType.Vec3);
        SetShaderValue(litShader, GetShaderLocation(litShader, "basisY"), Vector3.Cross(right, lightDir), ShaderUniformDataType.Vec3);
    }

    static Mesh[] Upload(U.Mesh shape)
    {
        if (sectors.TryGetValue(shape, out var meshes))
            return meshes;
        meshes = new Mesh[shape.tris.Length];
        for (int part = 0; part < meshes.Length; part++)
        {
            var indices = shape.tris[part];
            var mesh = new Mesh(indices.Length, indices.Length / 3);
            mesh.AllocVertices();
            mesh.AllocNormals();
            var vertices = mesh.VerticesAs<Vector3>();
            var normals = mesh.NormalsAs<Vector3>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = World(shape.vertices[indices[i]]);
                var b = World(shape.vertices[indices[i + 2]]);
                var c = World(shape.vertices[indices[i + 1]]);
                vertices[i] = a;
                vertices[i + 1] = b;
                vertices[i + 2] = c;
                normals[i] = normals[i + 1] = normals[i + 2] = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            }

            UploadMesh(ref mesh, false);
            meshes[part] = mesh;
        }

        sectors.Add(shape, meshes);
        return meshes;
    }

    public static void Release(U.Mesh mesh)
    {
        if (sectors.Remove(mesh, out var parts))
            foreach (var part in parts)
                UnloadMesh(part);
    }

    static Color SurfaceColor(U.Material material)
    {
        var color = U.GUI.Bytes(material.color);
        color.A = (byte)(255 * material.sheen);
        return color;
    }

    public void Render(U.Camera eye)
    {
        Rlgl.SetClipPlanes(eye.nearClipPlane, eye.farClipPlane);
        draws.Clear();
        text.Clear();
        SetShaderValue(litShader, eyeUniform, World(eye.transform.position), ShaderUniformDataType.Vec3);
        SetShaderValue(litShader, flashUniform, new Vector4(World(flash.transform.position), flash.gameObject.active ? flash.intensity * .35f : 0), ShaderUniformDataType.Vec4);
        foreach (var obj in U.GameObject.all)
        {
            if (!obj.active)
                continue;
            var label = obj.GetComponent<U.TextMesh>();
            if (label != null)
            {
                text.Add(label);
                continue;
            }

            var renderer = obj.GetComponent<U.MeshRenderer>();
            if (renderer == null)
                continue;
            var matrix = Matrix4x4.Transpose(reflectZ * obj.transform.world * reflectZ);
            bool castsShadow = renderer.shadowCastingMode != U.Rendering.ShadowCastingMode.Off;
            if (obj.primitive != null)
                draws.Add((primitives[obj.primitive], matrix, SurfaceColor(renderer.sharedMaterial), castsShadow));
            else if (obj.GetComponent<U.MeshFilter>()?.sharedMesh is U.Mesh shape)
            {
                var parts = Upload(shape);
                for (int i = 0; i < parts.Length; i++)
                    draws.Add((parts[i], matrix, SurfaceColor(renderer.sharedMaterials[i]), castsShadow));
            }
        }

        BeginTextureMode(shadowMap);
        ClearBackground(Color.White);
        BeginMode3D(lightCamera);
        foreach (var d in draws)
            if (d.castsShadow) DrawMesh(d.mesh, shadow, d.transform);
        EndMode3D();
        EndTextureMode();
        var camera = new Camera3D(World(eye.transform.position), World(eye.transform.position + eye.transform.forward), Vector3.UnitY, eye.fieldOfView, CameraProjection.Perspective);
        ClearBackground(U.GUI.Bytes(eye.backgroundColor));
        BeginMode3D(camera);
        foreach (var d in draws)
        {
            lit.Maps[0].Color = d.color;
            DrawMesh(d.mesh, lit, d.transform);
        }

        foreach (var label in text)
            DrawWorldText(label);
        EndMode3D();
    }

    static void DrawWorldText(U.TextMesh text)
    {
        var font = U.GUI.Regular;
        float em = text.characterSize * text.fontSize * .1f, units = em / font.BaseSize;
        float x = -MeasureTextEx(font, text.text, em, 0).X * .5f;
        var color = U.GUI.Bytes(text.color);
        var transform = text.transform.world;
        Rlgl.SetTexture(font.Texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
        foreach (char ch in text.text)
        {
            int id = GetGlyphIndex(font, ch);
            var g = font.Glyphs[id];
            var box = font.Recs[id];
            float left = x + g.OffsetX * units, top = em * .5f - g.OffsetY * units;
            float right = left + box.Width * units, bottom = top - box.Height * units;
            void Corner(float px, float py, float tx, float ty)
            {
                var p = Vector3.Transform(new(px, py, 0), transform);
                Rlgl.TexCoord2f(tx / font.Texture.Width, ty / font.Texture.Height);
                Rlgl.Vertex3f(p.X, p.Y, -p.Z);
            }

            Corner(left, top, box.X, box.Y);
            Corner(left, bottom, box.X, box.Y + box.Height);
            Corner(right, bottom, box.X + box.Width, box.Y + box.Height);
            Corner(right, top, box.X + box.Width, box.Y);
            x += (g.AdvanceX > 0 ? g.AdvanceX : box.Width) * units;
        }

        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    public void Dispose()
    {
        foreach (var mesh in sectors.Keys.ToArray())
            Release(mesh);
        foreach (var mesh in primitives.Values)
            UnloadMesh(mesh);
        // Default material maps refer to the shared white texture and our shadow target.
        MemFree(lit.Maps);
        MemFree(shadow.Maps);
        UnloadShader(litShader);
        UnloadShader(depthShader);
        UnloadRenderTexture(shadowMap);
    }
}
