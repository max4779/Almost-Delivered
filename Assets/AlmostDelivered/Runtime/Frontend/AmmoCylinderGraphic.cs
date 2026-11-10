using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Frontend
{
    public sealed class AmmoCylinderGraphic : MaskableGraphic
    {
        [SerializeField, Range(0, 6)] private int rounds = 6;
        public int Rounds
        {
            get => rounds;
            set
            {
                var next = Mathf.Clamp(value, 0, 6);
                if (rounds == next) return;
                rounds = next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var center = rectTransform.rect.center;
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .47f;
            Disc(mesh, center, radius, new Color(.08f, .12f, .16f, 1f));
            Disc(mesh, center, radius * .82f, new Color(.26f, .32f, .36f, 1f));
            Disc(mesh, center, radius * .23f, new Color(.08f, .12f, .16f, 1f));
            for (var i = 0; i < 6; i++)
            {
                var angle = (i * 60f + 30f) * Mathf.Deg2Rad;
                var slot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .57f;
                Disc(mesh, slot, radius * .21f, new Color(.025f, .04f, .055f, 1f));
                if (i < rounds)
                {
                    Disc(mesh, slot, radius * .15f, new Color(.94f, .7f, .3f, 1f));
                    Disc(mesh, slot, radius * .07f, new Color(1f, .9f, .62f, 1f));
                }
            }
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color32 tint)
        {
            const int segments = 20;
            var start = mesh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = center;
            mesh.AddVert(vertex);
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertex.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                mesh.AddVert(vertex);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
