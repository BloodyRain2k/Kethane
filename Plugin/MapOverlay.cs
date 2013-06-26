﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal class MapOverlay : MonoBehaviour
    {
        private CelestialBody body;
        private GeodesicGrid grid;
        private Dictionary<GeodesicGrid.Cell, Vector3d> cache = new Dictionary<GeodesicGrid.Cell, Vector3d>();
        private Mesh mesh;
        private GUISkin skin;
        private GeodesicGrid.Cell? hoverCell;

        public void Awake()
        {
            var scene = HighLogic.LoadedScene;
            if (scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION)
            {
                enabled = false;
            }
        }

        public void Start()
        {
            setUpMesh();
            gameObject.layer = 10;
            ScaledSpace.AddScaledSpaceTransform(gameObject.transform);
        }

        public void Update()
        {
            if (!MapView.MapIsEnabled)
            {
                gameObject.renderer.enabled = false;
                return;
            }

            gameObject.renderer.enabled = true;

            var target = MapView.fetch.mapCamera.target;

            var newBody = getTargetBody(target);
            if (newBody != body)
            {
                body = newBody;
                gameObject.transform.localScale = Vector3.one * (float)(1.025 * body.Radius / ScaledSpace.ScaleFactor);
            }

            var ray = MapView.MapCamera.camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, (ray.origin - gameObject.transform.position).magnitude * 2, 1 << 10) && (hitInfo.transform == gameObject.transform))
            {
                var hitVertex = triangleToVertexBase(hitInfo.triangleIndex);
                var sum = Enumerable.Range(hitVertex, 6).Select(c => mesh.vertices[c]).Aggregate((a, b) => a + b);
                hoverCell = grid.NearestCell(sum, cache);
            }
            else
            {
                hoverCell = null;
            }

            gameObject.transform.position = ScaledSpace.LocalToScaledSpace(body.position);
            gameObject.transform.rotation = body.rotation;
        }

        public void OnGUI()
        {
            if (skin == null)
            {
                GUI.skin = null;
                skin = (GUISkin)GameObject.Instantiate(GUI.skin);

                var window = skin.window;
                window.padding = new RectOffset(6, 6, 20, 6);
                window.fontSize = 10;
                skin.window = window;

                var label = skin.label;
                label.margin = new RectOffset(1, 1, 1, 1);
                label.padding = new RectOffset(1, 1, 1, 1);
                label.fontSize = 10;
                skin.label = label;
            }

            GUI.skin = skin;

            if (hoverCell != null)
            {
                var mouse = Event.current.mousePosition;
                var position = new Rect(mouse.x + 16, mouse.y + 4, 130, 55);
                GUILayout.Window(12359, position, mouseWindow, "Deposit");
            }
        }

        private void mouseWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cell: ");
            GUILayout.FlexibleSpace();
            GUILayout.Label(hoverCell.ToString());
            GUILayout.EndHorizontal();
        }

        private static CelestialBody getTargetBody(MapObject target)
        {
            if (target.type == MapObject.MapObjectType.CELESTIALBODY)
            {
                return target.celestialBody;
            }
            else if (target.type == MapObject.MapObjectType.MANEUVERNODE)
            {
                return target.maneuverNode.patch.referenceBody;
            }
            else if (target.type == MapObject.MapObjectType.VESSEL)
            {
                return target.vessel.mainBody;
            }

            return null;
        }

        private int triangleToVertexBase(int triangle)
        {
            if (triangle < 60)
            {
                return (triangle / 5) * 6;
            }
            else
            {
                return (triangle / 4) * 6 - 18;
            }
        }

        private void setUpMesh()
        {
            grid = new GeodesicGrid(5);
            var vertices = new List<UnityEngine.Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();

            foreach (var cell in grid.Pentagons.Concat(grid.Where(c => !c.IsPentagon)))
            {
                uv.AddRange(Enumerable.Repeat(new Vector2(0.25f, 0.25f), 6));

                var neighbors = cell.Neighbors.ToArray();

                for (var i = 0; i < neighbors.Length; i++)
                {
                    var a = neighbors[i];
                    var b = neighbors[i == neighbors.Length - 1 ? 0 : (i + 1)];

                    var center = (a.GetPosition(cache) + b.GetPosition(cache) + cell.GetPosition(cache)).normalized;

                    var blend = 0.08f;
                    vertices.Add(cell.GetPosition(cache) * blend + center * (1 - blend));
                }

                if (cell.IsPentagon)
                {
                    vertices.Add(cell.GetPosition(cache));
                    var t = vertices.Count - 6;
                    for (var i = 0; i < 5; i++)
                    {
                        triangles.AddRange(new int[] { t + 5, t + (i + 1) % 5, t + i });
                    }
                }
                else
                {
                    var t = vertices.Count - 6;
                    triangles.AddRange(new int[] { t + 2, t + 1, t, t + 4, t + 3, t + 2, t, t + 5, t + 4, t + 4, t + 2, t });
                }
            }

            mesh = gameObject.AddComponent<MeshFilter>().mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.normals = mesh.vertices;
            mesh.uv = uv.ToArray();
            mesh.Optimize();

            renderer.enabled = false;
            renderer.castShadows = false;
            renderer.receiveShadows = false;

            var material = new Material(Shader.Find("KSP/Alpha/Unlit Transparent"));
            material.mainTexture = GameDatabase.Instance.GetTexture("Kethane/hex", false);

            var color = Color.white;
            color.a = 0.25f;
            material.color = color;

            renderer.material = material;

            var collider = gameObject.AddComponent<MeshCollider>();
            collider.isTrigger = true;
        }
    }
}
