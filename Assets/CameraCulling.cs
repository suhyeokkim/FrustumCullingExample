namespace Safari
{
    using System;
    using System.Runtime.InteropServices;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public enum Caculation
    {
        Auto,
        ComputeShader,
        Script,
    }

    [RequireComponent(typeof(Camera))]
    public class CameraCulling : MonoBehaviour
    {
        public new Camera camera;

        [SerializeField]
        public ComputeShader cullingComputeShader;
        
        [SerializeField]
        public Renderer[] staticRenderers;

        [NonSerialized]
        public int boundCullHash;

        [NonSerialized]
        public uint threadSizeX;

        [SerializeField]
        public Bounds[] staticRendererBounds;
        public ComputeBuffer boundGPUBuffer;

        [NonSerialized]
        public int[] cullingResults;
        public ComputeBuffer resultGPUBuffer;

        [NonSerialized]
        public Plane[] frustomPlanes;
        public ComputeBuffer planeGPUBUffer;

        [ContextMenu("Get static renderers")]
        private void GetStaticRenderers()
        {
            staticRenderers = Array.FindAll(FindObjectsOfType<Renderer>(), (renderer) => renderer.gameObject.isStatic);
            staticRendererBounds = Array.ConvertAll(staticRenderers, (renderer) => renderer.bounds);

            Debug.Log(staticRendererBounds.Length);
        }

        [ContextMenu("Get camera")]
        private void GetCamera()
        {
            camera = GetComponent<Camera>();
        }

        private void Init()
        {
            boundCullHash = cullingComputeShader.FindKernel("OverlapFrustomAndBounds");

            if (boundCullHash < 0)
            {
                Debug.LogError("CameraCulling.Init >> frustom and bound kernel not exist..");
            }

            uint threadSizeY;
            uint threadSizeZ;

            cullingComputeShader.GetKernelThreadGroupSizes(boundCullHash, out threadSizeX, out threadSizeY, out threadSizeZ);

            if (staticRendererBounds == null)
                staticRendererBounds = Array.ConvertAll(staticRenderers, (renderer) => renderer.bounds);

            frustomPlanes = new Plane[6];
            planeGPUBUffer = new ComputeBuffer(6, Marshal.SizeOf(typeof(Plane)));
            planeGPUBUffer.SetData(frustomPlanes);

            boundGPUBuffer = new ComputeBuffer(staticRenderers.Length, Marshal.SizeOf(typeof(Bounds)));
            boundGPUBuffer.SetData(staticRendererBounds);

            if (cullingResults == null)
                cullingResults = new int[staticRenderers.Length];

            resultGPUBuffer = new ComputeBuffer(staticRenderers.Length, Marshal.SizeOf(typeof(uint)));
            resultGPUBuffer.SetData(cullingResults);

            cullingComputeShader.SetInt("resultLength", staticRenderers.Length);

            cullingComputeShader.SetBuffer(boundCullHash, "bounds", boundGPUBuffer);
            cullingComputeShader.SetBuffer(boundCullHash, "planes", planeGPUBUffer);
            cullingComputeShader.SetBuffer(boundCullHash, "results", resultGPUBuffer);
        }

        public void Cull()
        {
            GeometryUtility.CalculateFrustumPlanes(camera, frustomPlanes);
            planeGPUBUffer.SetData(frustomPlanes);

            uint length = (uint)staticRenderers.Length;
            uint dispatchXLength = length / threadSizeX + (uint)(((int)length % (int)threadSizeX > 0) ? 1 : 0);

            cullingComputeShader.Dispatch(boundCullHash, (int)dispatchXLength, 1, 1);
            resultGPUBuffer.GetData(cullingResults);

            /// 1000 내외까지 가능
            for (int i = 0; i < staticRenderers.Length; i++)
                staticRenderers[i].enabled = cullingResults[i] != 0;
        }

        private void Awake()
        {
            Init();

            prevPos = camera.transform.position;
            prevRot = camera.transform.rotation;

            Cull();
        }

        private Vector3 prevPos;
        private Quaternion prevRot;

        private void OnPreCull()
        {
            if (prevPos != camera.transform.position || prevRot != camera.transform.rotation)
            {
                prevPos = camera.transform.position;
                prevRot = camera.transform.rotation;

                Cull();
            }
        }

        public float moveValue = 5f;

        public void Update()
        {
            Vector3 rotation = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
                rotation.x -= moveValue * Time.deltaTime;
            else if (Input.GetKey(KeyCode.S))
                rotation.x += moveValue * Time.deltaTime;

            if (Input.GetKey(KeyCode.A))
                rotation.y -= moveValue * Time.deltaTime;
            else if (Input.GetKey(KeyCode.D))
                rotation.y += moveValue * Time.deltaTime;

            transform.Rotate(rotation);
        }
    }
}