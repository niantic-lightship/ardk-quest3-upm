using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Occlusion;
using UnityEngine;
using UnityEngine.Rendering;
#if MODULE_URP_ENABLED
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AROcclusionManager))]
    public class OcclusionMesh : ConditionalRenderer
    {
        [Tooltip("Whether to disable occlusions performed by the AR Occlusion Manager to avoid duplicating the workload.")]
        [SerializeField]
        private bool _disableSystemOcclusion = true;

        // Shader property bindings
        private static readonly int s_colorMaskId = Shader.PropertyToID("_ColorMask");
        private static readonly int s_imageWidthId = Shader.PropertyToID("_ImageWidth");
        private static readonly int s_imageHeightId = Shader.PropertyToID("_ImageHeight");
        private static readonly int s_depthTextureId = Shader.PropertyToID("_DepthTexture");
        private static readonly int s_intrinsicsId = Shader.PropertyToID("_Intrinsics");
        private static readonly int s_extrinsicsId = Shader.PropertyToID("_Extrinsics");
        private static readonly int s_ndcToLinearDepthParamsId = Shader.PropertyToID("_NdcToLinearDepthParams");
        private static readonly int s_cameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");

        // Keywords
        private const string StereoDepthKeyword = "STEREO_DEPTH";
        private const string NonLinearDepthKeyword = "NON_LINEAR_DEPTH";

        /// <summary>
        /// The name of the shader used by the rendering material.
        /// </summary>
        protected override string ShaderName => "Lightship/OcclusionMeshStereo";

        /// <summary>
        /// The name of this renderer.
        /// </summary>
        protected override string RendererName => "OcclusionMeshRenderer";

        // Required components
        private AROcclusionManager _occlusionManager;

        // Resources
        private Mesh _mesh;
        private Vector2Int? _textureSize;

#if MODULE_URP_ENABLED
        // URP render pass
        private MeshRenderingPass _renderPass;

        // Skip command buffer creation in URP
        protected override bool ShouldAddCommandBuffer => false;
#endif

        private enum ColorMask
        {
            None = 0, // RGBA: 0000
            Depth = 5, // RGBA: 0101
            UV = 11, // RGBA: 1011
            All = 15, // RGBA: 1111
        }
        private ColorMask _colorMask = ColorMask.None;

        /// <summary>
        /// Get or set whether to visualize the depth data.
        /// </summary>
        public bool DebugVisualization
        {
            get => _colorMask != ColorMask.None;
            set
            {
                _colorMask = value ? ColorMask.Depth : ColorMask.None;
            }
        }

        /// <summary>
        /// Invoked when the material state needs to be reset or initialized with
        /// default values. This usually occurs when the material is first created.
        /// </summary>
        /// <param name="mat"></param>
        protected override void OnInitializeMaterial(Material mat)
        {
            base.OnInitializeMaterial(mat);

#if (!UNITY_EDITOR && NIANTIC_LIGHTSHIP_META_ENABLED)
            mat.EnableKeyword(StereoDepthKeyword);
            mat.EnableKeyword(NonLinearDepthKeyword);
#else
            mat.DisableKeyword(StereoDepthKeyword);
            mat.DisableKeyword(NonLinearDepthKeyword);
#endif
        }

        /// <summary>
        /// Invoked when it is time to add rendering commands to the command buffer.
        /// </summary>
        /// <param name="cmd">The command buffer resource.</param>
        /// <param name="mat">The material that should be used to draw the frame.</param>
        /// <returns>Whether the command buffer could be successfully configured.</returns>
        protected override bool OnAddRenderCommands(CommandBuffer cmd, Material mat)
        {
            var mesh = GetOrCreateMesh();
            if (mesh == null)
            {
                return false;
            }

            cmd.Clear();
            cmd.DrawMesh(mesh, Matrix4x4.identity, Material);
            return true;
        }

        /// <summary>
        /// Invoked to query the external command buffers that need to run before our own.
        /// </summary>
        /// <param name="evt">The camera event to search command buffers for.</param>
        /// <returns>Names or partial names of the command buffers.</returns>
        protected override string[] OnRequestExternalPassDependencies(CameraEvent evt)
        {
#if !UNITY_EDITOR && (NIANTIC_LIGHTSHIP_ML2_ENABLED || NIANTIC_LIGHTSHIP_META_ENABLED)
            // We don't expect built-in command buffers (e.g. background rendering) on Meta Quest
            return null;
#endif
            // The AR Background Pass is not required for this effect,
            // but we have to schedule ourselves after it, if it is present.
            return GetComponent<ARCameraBackground>() != null ? new[] {"AR Background"} : null;
        }

        protected override void Awake()
        {
            base.Awake();

            // Acquire components
            _occlusionManager = GetComponent<AROcclusionManager>();

            // Disable system occlusion
            if (_disableSystemOcclusion)
            {
                _occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
            }

#if MODULE_URP_ENABLED
            // Allocate the render pass
            _renderPass = new MeshRenderingPass(RendererName,
                // ARF background pass performs at RenderPassEvent.BeforeRenderingOpaques,
                // in the editor, so we use the next event to ensure we render after it.
                // TODO(ahegedus): consider creating a render feature asset instead
                RenderPassEvent.BeforeRenderingOpaques + 1);
#endif
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _occlusionManager.frameReceived += OnOcclusionFrameReceived;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _occlusionManager.frameReceived -= OnOcclusionFrameReceived;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        private void OnOcclusionFrameReceived(AROcclusionFrameEventArgs args)
        {
            if (args.externalTextures.Count > 0)
            {
                // Update the depth texture
                var texture = args.externalTextures[0].texture;

                // Store the image size for mesh creation
                _textureSize ??= new Vector2Int(texture.width, texture.height);

                // Bind the texture and its properties
                Material.SetInt(s_imageWidthId, texture.width);
                Material.SetInt(s_imageHeightId, texture.height);
                Material.SetTexture(s_depthTextureId, texture);
                Material.SetFloat(s_colorMaskId, (int)_colorMask);

                // Bind intrinsics
                if (args.TryGetFovs(out var fieldOfView))
                {
                    var intrinsicsArray = new Vector4[fieldOfView.Count];
                    for (int i = 0; i < fieldOfView.Count; i++)
                    {
                        var intrinsics = CalculateIntrinsics(fieldOfView[i], texture.width, texture.height);
                        intrinsicsArray[i] = new Vector4(
                            intrinsics.focalLength.x,
                            intrinsics.focalLength.y,
                            intrinsics.principalPoint.x,
                            // Invert cy because the native image is flipped vertically
                            intrinsics.resolution.y - intrinsics.principalPoint.y);
                    }

                    Material.SetVectorArray(s_intrinsicsId, intrinsicsArray);
                }

                // Bind extrinsics
                if (args.TryGetPoses(out var poses))
                {
                    Vector3 coordSystemScale = new(1, 1, -1);
                    var extrinsicsArray = new Matrix4x4[poses.Count];
                    for (int i = 0; i < poses.Count; i++)
                    {
                        var pose = poses[i];
                        extrinsicsArray[i] = Matrix4x4.TRS(pose.position, pose.rotation, coordSystemScale);
                    }

                    Material.SetMatrixArray(s_extrinsicsId, extrinsicsArray);
                }

                // Bind NDC to linear depth parameters
                if (args.TryGetNearFarPlanes(out var planes))
                {
                    var ndcToLinearDepthParams = GetNdcToLinearDepthParameters(planes.nearZ, planes.farZ);
                    Material.SetVector(s_ndcToLinearDepthParamsId, ndcToLinearDepthParams);
                }

                // Set scale: this computes the affect the camera's localToWorld has on the length of the
                // forward vector, i.e., how much farther from the camera are things than with unit scale.
                var forward = Camera.transform.localToWorldMatrix.GetColumn(2);
                var cameraForwardScale = forward.magnitude;
                Material.SetFloat(s_cameraForwardScaleId, cameraForwardScale);
            }
        }

        /// <summary>
        /// Gets or creates the mesh used for rendering the occluder.
        /// </summary>
        private Mesh GetOrCreateMesh()
        {
            if (_mesh == null)
            {
                if (!_textureSize.HasValue)
                {
                    return null;
                }

                _mesh = CreateGeometry(_textureSize.Value.x, _textureSize.Value.y);
            }

            return _mesh;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
#if MODULE_URP_ENABLED
            if (cam != Camera)
            {
                return;
            }

            var mesh = GetOrCreateMesh();
            if (mesh == null)
            {
                return;
            }

            // Configure the render pass
            _renderPass.SetMaterial(Material);
            _renderPass.SetMesh(_mesh);

            // Enqueue the render pass
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_renderPass);
#endif
        }

        /// <summary>
        /// Creates a mesh geometry that has a vertex for each pixel in the depth texture.
        /// </summary>
        private static Mesh CreateGeometry(int width, int height)
        {
            var numPoints = width * height;
            var vertices = new Vector3[numPoints];
            var numTriangles = 2 * (width - 1) * (height - 1); // just under 2 triangles per point, total

            // Map vertex indices to triangle in triplets
            var triangleIdx = new int[numTriangles * 3]; // 3 vertices per triangle
            var startIndex = 0;

            for (var i = 0; i < width * height; ++i)
            {
                var h = i / width;
                var w = i % width;
                if (h == height - 1 || w == width - 1)
                {
                    continue;
                }

                // Triangle indices are counter-clockwise to face you
                triangleIdx[startIndex] = i;
                triangleIdx[startIndex + 1] = i + width;
                triangleIdx[startIndex + 2] = i + width + 1;
                triangleIdx[startIndex + 3] = i;
                triangleIdx[startIndex + 4] = i + width + 1;
                triangleIdx[startIndex + 5] = i + 1;
                startIndex += 6;
            }

            var mesh = new Mesh
            {
                indexFormat = width * height >= 65534 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                triangles = triangleIdx
            };
            mesh.UploadMeshData(true);

            return mesh;
        }

        /// <summary>
        /// Calculates the parameters needed to linearize depth values.
        /// </summary>
        /// <remarks>This is copied from the AR Shader Occlusion.</remarks>
        private static Vector4 GetNdcToLinearDepthParameters(float near, float far)
        {
            float invDepthFactor;
            float depthOffset;

            if (far < near || float.IsInfinity(far))
            {
                invDepthFactor = -2.0f * near;
                depthOffset = -1.0f;
            }
            else
            {
                invDepthFactor = -2.0f * far * near / (far - near);
                depthOffset = -(far + near) / (far - near);
            }

            return new Vector4(invDepthFactor, depthOffset, 0, 0);
        }

        /// <summary>
        /// Calculates the camera calibration parameters.
        /// </summary>
        /// <param name="fov">Field of view.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <returns>The camera intrinsics.</returns>
        private static XRCameraIntrinsics CalculateIntrinsics(XRFov fov, int width, int height)
        {
            // Convert to tangents
            var tanLeft = Mathf.Tan(fov.angleLeft);
            var tanRight = Mathf.Tan(fov.angleRight);
            var tanUp = Mathf.Tan(fov.angleUp);
            var tanDown = Mathf.Tan(fov.angleDown);

            // Calculate the full focal lengths
            float spanX = Mathf.Abs(tanLeft) + Mathf.Abs(tanRight);
            float spanY = Mathf.Abs(tanUp) + Mathf.Abs(tanDown);

            return new XRCameraIntrinsics(
                focalLength: new Vector2(width / spanX, height / spanY),
                principalPoint: new Vector2(width * Mathf.Abs(tanLeft) / spanX, height * Mathf.Abs(tanUp) / spanY),
                resolution: new Vector2Int(width, height));
        }
    }
}
