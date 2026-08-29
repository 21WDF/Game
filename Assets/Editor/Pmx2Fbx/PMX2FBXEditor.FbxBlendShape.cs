#if AUTODESK_FBX
using Autodesk.Fbx;
#endif
using System;
using System.IO;
using UnityEngine;

namespace PMX2FBX
{
    public partial class PMX2FBXEditor
    {
        void AddEmptyBlendShapesToFbx(string fbxAbsPath)
        {
#if !AUTODESK_FBX
            Debug.LogWarning("[PMX2FBX] com.autodesk.fbx 包未安装，无法写入空白 BlendShape。");
            return;
#else
            if (!File.Exists(fbxAbsPath)) return;

            var manager = FbxManager.Create();
            FbxIOSettings ioSettings = null;
            FbxImporter importer = null;
            FbxScene scene = null;
            FbxExporter exporter = null;

            try
            {
                ioSettings = FbxIOSettings.Create(manager, Globals.IOSROOT);
                manager.SetIOSettings(ioSettings);

                importer = FbxImporter.Create(manager, "importer");
                if (!importer.Initialize(fbxAbsPath, -1, manager.GetIOSettings()))
                {
                    Debug.LogWarning($"[PMX2FBX] FBX Initialize 失败，跳过写入空白 BlendShape: {importer.GetStatus().GetErrorString()}");
                    return;
                }

                int fileFormat = DetectWriterFormat(manager, fbxAbsPath);
                scene = FbxScene.Create(manager, "scene");
                importer.Import(scene);
                importer.Destroy();
                importer = null;

                int meshCount = 0, channelAdded = 0;
                var root = scene.GetRootNode();
                if (root != null)
                    ProcessNodeRecursive(scene, root, ref meshCount, ref channelAdded);

                if (channelAdded > 0)
                {
                    exporter = FbxExporter.Create(manager, "exporter");
                    if (!exporter.Initialize(fbxAbsPath, fileFormat, manager.GetIOSettings()))
                    {
                        Debug.LogWarning($"[PMX2FBX] FBX 导出初始化失败，跳过写入空白 BlendShape: {exporter.GetStatus().GetErrorString()}");
                    }
                    else
                    {
                        exporter.Export(scene);
                    }
                    exporter.Destroy();
                    exporter = null;
                    Log($"✓ 空白 BlendShape \"{_blendShapeName}\" 已写入 ({channelAdded} 个网格)", "#4ADE80");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PMX2FBX] AddEmptyBlendShapesToFbx 失败: {ex.Message}");
            }
            finally
            {
                if (importer != null) importer.Destroy();
                if (scene != null) scene.Destroy();
                if (exporter != null) exporter.Destroy();
                if (ioSettings != null) ioSettings.Destroy();
                manager.Destroy();
            }
#endif
        }

#if AUTODESK_FBX

        int DetectWriterFormat(FbxManager manager, string path)
        {
            bool isBinary = false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);
                string header = System.Text.Encoding.ASCII.GetString(br.ReadBytes(20));
                isBinary = header.StartsWith("Kaydara FBX Binary");
            }
            catch { isBinary = true; }

            var registry = manager.GetIOPluginRegistry();
            return isBinary
                ? registry.FindWriterIDByDescription("FBX binary (*.fbx)")
                : registry.FindWriterIDByDescription("FBX ascii (*.fbx)");
        }

        void ProcessNodeRecursive(FbxScene scene, FbxNode node, ref int meshCount, ref int channelAdded)
        {
            var attr = node.GetNodeAttribute();
            if (attr != null && attr.GetAttributeType() == FbxNodeAttribute.EType.eMesh)
            {
                var mesh = node.GetMesh();
                if (mesh != null)
                {
                    meshCount++;
                    bool match = string.IsNullOrEmpty(_blendShapeMeshFilter)
                        || node.GetName() == _blendShapeMeshFilter
                        || mesh.GetName() == _blendShapeMeshFilter;
                    if (match && AddEmptyBlendShapeToMesh(scene, mesh))
                        channelAdded++;
                }
            }
            for (int i = 0; i < node.GetChildCount(); i++)
                ProcessNodeRecursive(scene, node.GetChild(i), ref meshCount, ref channelAdded);
        }

        bool AddEmptyBlendShapeToMesh(FbxScene scene, FbxMesh mesh)
        {
            int count = mesh.GetControlPointsCount();
            if (count <= 0) return false;

            FbxBlendShape blendShape = null;
            int dc = mesh.GetDeformerCount(FbxDeformer.EDeformerType.eBlendShape);
            if (dc > 0) blendShape = mesh.GetBlendShapeDeformer(0);

            if (blendShape == null)
            {
                blendShape = FbxBlendShape.Create(scene, mesh.GetName() + "_BlendShapes");
                mesh.AddDeformer(blendShape);
            }

            for (int i = 0; i < blendShape.GetBlendShapeChannelCount(); i++)
            {
                var ch = blendShape.GetBlendShapeChannel(i);
                if (ch != null && ch.GetName() == _blendShapeName) return false;
            }

            var channel = FbxBlendShapeChannel.Create(scene, _blendShapeName);
            var shape = FbxShape.Create(scene, _blendShapeName + "_Shape");
            shape.InitControlPoints(count);
            for (int i = 0; i < count; i++)
                shape.SetControlPointAt(mesh.GetControlPointAt(i), i);

            channel.AddTargetShape(shape, 100.0);
            blendShape.AddBlendShapeChannel(channel);
            return true;
        }

#endif
    }
}
