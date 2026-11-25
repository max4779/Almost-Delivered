using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>A top view of the hovering cargo: hull, canister, four thrusters and energy core.</summary>
    public sealed class CargoBlueprintGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 3)] private int destructions;
        private Vector2 origin;
        private float scale;

        private static readonly Color32 Grid = new Color(.23f, .55f, .62f, .13f);
        private static readonly Color32 Guide = new Color(.34f, .68f, .72f, .34f);
        private static readonly Color32 LineColor = new Color(.37f, .83f, .9f, .91f);
        private static readonly Color32 FineLine = new Color(.32f, .69f, .76f, .65f);
        private static readonly Color32 HullFill = new Color(.025f, .095f, .13f, .97f);
        private static readonly Color32 ModuleFill = new Color(.045f, .18f, .22f, .92f);
        private static readonly Color32 AlertLine = new Color(1f, .25f, .19f, .97f);
        private static readonly Color32 AlertFill = new Color(.47f, .045f, .045f, .91f);
        private static readonly Color32 AlertCore = new Color(1f, .13f, .09f, 1f);
        private static readonly Color32 SensorFill = new Color(.62f, .4f, .08f, .94f);
        private static readonly Color32 SensorLine = new Color(1f, .8f, .3f, 1f);

        public int Destructions
        {
            get => destructions;
            set
            {
                var next = Mathf.Clamp(value, 0, 3);
                if (destructions == next) return;
                destructions = next;
                SetVerticesDirty();
            }
        }

        public bool IsDamaged(int region)
        {
            switch (region)
            {
                case 0: return destructions >= 1; // left thruster bank
                case 2: return destructions >= 2; // right thruster bank
                case 1: return destructions >= 3; // central canister and core
                default: return false;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            origin = rect.center;
            scale = Mathf.Min(rect.width / 300f, rect.height / 170f);

            DrawGrid(mesh);
            DrawSideBank(mesh, -1f, IsDamaged(0));
            DrawSideBank(mesh, 1f, IsDamaged(2));
            DrawHull(mesh, IsDamaged(1));
            DrawDimensions(mesh);
        }

        private void DrawGrid(VertexHelper mesh)
        {
            for (var x = -140f; x <= 140f; x += 20f)
                Line(mesh, new Vector2(x, -69f), new Vector2(x, 69f), .5f, Grid);
            for (var y = -60f; y <= 60f; y += 20f)
                Line(mesh, new Vector2(-145f, y), new Vector2(145f, y), .5f, Grid);
            Line(mesh, new Vector2(0f, -72f), new Vector2(0f, 72f), .7f, Guide);
            Line(mesh, new Vector2(-145f, 0f), new Vector2(145f, 0f), .7f, Guide);
        }

        private void DrawSideBank(VertexHelper mesh, float side, bool damaged)
        {
            var edge = damaged ? AlertLine : LineColor;
            var fill = damaged ? AlertFill : ModuleFill;
            var plate = new[]
            {
                new Vector2(side * 126f, 40f), new Vector2(side * 116f, 51f),
                new Vector2(side * 85f, 51f), new Vector2(side * 75f, 39f),
                new Vector2(side * 75f, -39f), new Vector2(side * 85f, -51f),
                new Vector2(side * 116f, -51f), new Vector2(side * 126f, -40f)
            };
            Polygon(mesh, plate, fill);
            Outline(mesh, plate, 2f, edge);

            for (var index = 0; index < 2; index++)
            {
                var y = index == 0 ? 30f : -30f;
                var center = new Vector2(side * 101f, y);
                Disc(mesh, center, 17f, HullFill);
                Ring(mesh, center, 17f, 2f, edge);
                Ring(mesh, center, 12f, 1.2f, FineLine);
                Disc(mesh, center, 7f, damaged ? AlertCore : ModuleFill);
                Ring(mesh, center, 7f, 1f, edge);
                for (var blade = 0; blade < 4; blade++)
                {
                    var angle = blade * Mathf.PI * .5f + Mathf.PI * .25f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Line(mesh, center + direction * 7f, center + direction * 11f, 1.2f, edge);
                }
            }

            Line(mesh, new Vector2(side * 83f, 10f), new Vector2(side * 119f, 10f), 1f, edge);
            Line(mesh, new Vector2(side * 83f, -10f), new Vector2(side * 119f, -10f), 1f, edge);
            Line(mesh, new Vector2(side * 75f, 36f), new Vector2(side * 60f, 36f), 2.5f, edge);
            Line(mesh, new Vector2(side * 75f, -36f), new Vector2(side * 60f, -36f), 2.5f, edge);
            Disc(mesh, new Vector2(side * 78f, 0f), 5.7f, damaged ? AlertCore : LineColor);
            Ring(mesh, new Vector2(side * 78f, 0f), 9f, 1.3f, edge);

            if (!damaged) return;
            Line(mesh, new Vector2(side * 121f, 42f), new Vector2(side * 80f, -42f), 2.6f, AlertLine);
            Line(mesh, new Vector2(side * 80f, 42f), new Vector2(side * 121f, -42f), 2.6f, AlertLine);
        }

        private void DrawHull(VertexHelper mesh, bool damaged)
        {
            var edge = damaged ? AlertLine : LineColor;
            var hull = new[]
            {
                new Vector2(-66f, -47f), new Vector2(66f, -47f),
                new Vector2(66f, 38f), new Vector2(47f, 52f),
                new Vector2(20f, 70f), new Vector2(0f, 80f),
                new Vector2(-20f, 70f), new Vector2(-47f, 52f),
                new Vector2(-66f, 38f)
            };
            Polygon(mesh, hull, HullFill);
            Outline(mesh, hull, 2.5f, edge);
            var canister = new[]
            {
                new Vector2(-43f, 37f), new Vector2(43f, 37f),
                new Vector2(51f, 25f), new Vector2(51f, -29f),
                new Vector2(39f, -42f), new Vector2(-39f, -42f),
                new Vector2(-51f, -29f), new Vector2(-51f, 25f)
            };
            Polygon(mesh, canister, damaged ? AlertFill : ModuleFill);
            Outline(mesh, canister, 1.6f, edge);

            for (var side = -1f; side <= 1f; side += 2f)
            {
                Line(mesh, new Vector2(side * 60f, 42f), new Vector2(side * 47f, 31f), 1.3f, edge);
                Line(mesh, new Vector2(side * 61f, -47f), new Vector2(side * 45f, -34f), 1.3f, edge);
                Disc(mesh, new Vector2(side * 56f, 0f), 2.5f, edge);
                for (var vent = 0; vent < 3; vent++)
                    Line(mesh, new Vector2(side * 49f, 16f - vent * 16f),
                        new Vector2(side * 37f, 16f - vent * 16f), 1.1f, edge);
            }

            // A tapered sensor prow is part of the hull silhouette; the rear is squared off.
            var prow = new[]
            {
                new Vector2(-32f, 49f), new Vector2(32f, 49f),
                new Vector2(19f, 63f), new Vector2(0f, 75f), new Vector2(-19f, 63f)
            };
            Polygon(mesh, prow, damaged ? AlertFill : SensorFill);
            Outline(mesh, prow, 1.6f, damaged ? AlertLine : SensorLine);
            Line(mesh, new Vector2(-16f, 56f), new Vector2(16f, 56f), 2.2f,
                damaged ? AlertLine : SensorLine);
            Line(mesh, new Vector2(-34f, -47f), new Vector2(34f, -47f), 2.8f, edge);
            for (var x = -24f; x <= 24f; x += 12f)
                Line(mesh, new Vector2(x, -47f), new Vector2(x, -57f), 1.8f, FineLine);

            Disc(mesh, Vector2.zero, 24f, HullFill);
            Ring(mesh, Vector2.zero, 24f, 1.7f, edge);
            Ring(mesh, Vector2.zero, 18f, 1f, FineLine);
            Disc(mesh, Vector2.zero, 11f, damaged ? AlertCore : LineColor);
            Ring(mesh, Vector2.zero, 6f, 1.3f, damaged ? AlertLine : HullFill);
            for (var angle = 0; angle < 4; angle++)
            {
                var radians = angle * Mathf.PI * .5f + Mathf.PI * .25f;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Line(mesh, direction * 24f, direction * 37f, 1.4f, edge);
            }
            if (!damaged) return;
            Line(mesh, new Vector2(-38f, 35f), new Vector2(38f, -35f), 2.5f, AlertLine);
            Line(mesh, new Vector2(38f, 35f), new Vector2(-38f, -35f), 2.5f, AlertLine);
        }

        private void DrawDimensions(VertexHelper mesh)
        {
            for (var side = -1f; side <= 1f; side += 2f)
            {
                var x = side * 141f;
                Line(mesh, new Vector2(x, -56f), new Vector2(x, 56f), .8f, Guide);
                for (var y = -50f; y <= 50f; y += 10f)
                {
                    var length = Mathf.Abs(y % 50f) < .01f ? 8f : 4f;
                    Line(mesh, new Vector2(x, y), new Vector2(x - side * length, y), .9f, Guide);
                }
            }
        }

        private void Polygon(VertexHelper mesh, Vector2[] points, Color32 tint)
        {
            if (points.Length < 3) return;
            var start = mesh.currentVertCount;
            foreach (var point in points) AddVertex(mesh, point, tint);
            for (var index = 1; index < points.Length - 1; index++)
                mesh.AddTriangle(start, start + index, start + index + 1);
        }

        private void Outline(VertexHelper mesh, Vector2[] points, float width, Color32 tint)
        {
            for (var index = 0; index < points.Length; index++)
                Line(mesh, points[index], points[(index + 1) % points.Length], width, tint);
        }

        private void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color32 tint)
        {
            const int segments = 28;
            for (var index = 0; index < segments; index++)
            {
                var start = index * Mathf.PI * 2f / segments;
                var end = (index + 1) * Mathf.PI * 2f / segments;
                Line(mesh, center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius,
                    center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius, width, tint);
            }
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color32 tint)
        {
            const int segments = 28;
            var start = mesh.currentVertCount;
            AddVertex(mesh, center, tint);
            for (var index = 0; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                AddVertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
                if (index > 0) mesh.AddTriangle(start, start + index, start + index + 1);
            }
        }

        private void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color32 tint)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < .0001f) return;
            var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            var first = mesh.currentVertCount;
            AddVertex(mesh, start - normal, tint);
            AddVertex(mesh, start + normal, tint);
            AddVertex(mesh, end + normal, tint);
            AddVertex(mesh, end - normal, tint);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void AddVertex(VertexHelper mesh, Vector2 point, Color32 tint)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = origin + point * scale;
            mesh.AddVert(vertex);
        }
    }
}
