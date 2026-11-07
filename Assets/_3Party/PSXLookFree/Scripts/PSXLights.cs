using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// Hands the PSX shaders the point and spot lights they light per vertex, as URP lights those per
    /// pixel (or per cluster, in Forward+) only: before each frame, the 8 enabled ones nearest the
    /// first camera, as URP would light them. The main directional light and ambient are URP's own.
    /// </summary>
    static class PSXLights
    {
        const int Max = 8;  // PSX.hlsl's PSX_MAX_LIGHTS
        static readonly int Count = Shader.PropertyToID("_PSXLightCount"), Position = Shader.PropertyToID("_PSXLightPosition"),
            LightColor = Shader.PropertyToID("_PSXLightColor"), Spot = Shader.PropertyToID("_PSXLightSpot"),
            SpotOffset = Shader.PropertyToID("_PSXLightSpotOffset");
        static readonly Vector4[] position = new Vector4[Max], color = new Vector4[Max], spot = new Vector4[Max], spotOffset = new Vector4[Max];
        static readonly List<Light> near = new List<Light>();
        static Light[] lights = new Light[0];
        static float found = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            RenderPipelineManager.beginContextRendering -= Upload;
            RenderPipelineManager.beginContextRendering += Upload;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void HookEditor() => Hook();
#endif

        static void Upload(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (Time.realtimeSinceStartup - found > 1f)  // lights added or removed show within a second
            {
                lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                found = Time.realtimeSinceStartup;
            }
            var eye = cameras.Count > 0 ? cameras[0].transform.position : Vector3.zero;
            near.Clear();
            foreach (var l in lights)
                if (l && l.isActiveAndEnabled && l.intensity > 0f && (l.type == LightType.Point || l.type == LightType.Spot))
                    near.Add(l);
            near.Sort((a, b) => (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));
            int n = Mathf.Min(near.Count, Max);
            for (int i = 0; i < n; i++)
            {
                var l = near[i];
                var c = QualitySettings.activeColorSpace == ColorSpace.Linear ? l.color.linear * l.intensity : l.color * l.intensity;
                if (GraphicsSettings.lightsUseColorTemperature && l.useColorTemperature)
                    c *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature);
                position[i] = l.transform.position;
                position[i].w = 1f / Mathf.Max(l.range * l.range, 1e-4f);
                color[i] = c;
                spot[i] = Vector4.zero;
                spotOffset[i] = Vector4.one;
                if (l.type == LightType.Spot)
                {
                    float cosOuter = Mathf.Cos(Mathf.Deg2Rad * l.spotAngle * 0.5f), cosInner = Mathf.Cos(Mathf.Deg2Rad * l.innerSpotAngle * 0.5f);
                    float scale = 1f / Mathf.Max(0.001f, cosInner - cosOuter);
                    spot[i] = -l.transform.forward;
                    spot[i].w = scale;
                    spotOffset[i].x = -cosOuter * scale;
                }
            }
            Shader.SetGlobalInteger(Count, n);
            Shader.SetGlobalVectorArray(Position, position);
            Shader.SetGlobalVectorArray(LightColor, color);
            Shader.SetGlobalVectorArray(Spot, spot);
            Shader.SetGlobalVectorArray(SpotOffset, spotOffset);
        }
    }
}
