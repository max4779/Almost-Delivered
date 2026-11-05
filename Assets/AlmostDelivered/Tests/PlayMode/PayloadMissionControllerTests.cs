using System.Linq;
using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Payload;
using AlmostDelivered.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace AlmostDelivered.Tests.PlayMode
{
    public sealed class PayloadMissionControllerTests
    {
        private GameObject root;
        private GameObject player;
        private GameObject payload;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Payload controller test");
            player = Actor("Player", CombatTeamId.Player);
            payload = new GameObject("Payload");
            player.transform.SetParent(root.transform, false);
            payload.transform.SetParent(root.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void AuthorityInitializesRoutePayloadAndMapFromSameSeed()
        {
            var map = new GameObject("Map").AddComponent<MissionMapGenerator>();
            map.transform.SetParent(root.transform, false);
            var controller = NewController();

            controller.Configure(true, 4711, Bounds(), player.transform, payload.transform, map, null, 10f, 0f, 4.5f);

            Assert.That(controller.IsAuthority, Is.True);
            Assert.That(controller.RoutePlan.Points, Is.EqualTo(PayloadRoutePlanner.Generate(4711, Bounds(), 5, 3f).Points));
            Assert.That(payload.transform.position, Is.EqualTo(controller.RoutePlan.Points[0]));
            Assert.That(map.LastSeed, Is.EqualTo(4711));
        }

        [Test]
        public void LivingPlayerEscortsAndLivingEnemyContests()
        {
            var controller = NewController();
            controller.Configure(true, 12, Bounds(), player.transform, payload.transform, null, null, 10f, 0f, 4.5f);
            player.transform.position = payload.transform.position;
            Physics.SyncTransforms();

            controller.Tick(1f);
            Assert.That(controller.Snapshot.RouteDistance, Is.EqualTo(10f));
            Assert.That(payload.transform.position, Is.EqualTo(controller.RoutePlan.EvaluateDistance(10f)));

            player.transform.position = payload.transform.position;
            var enemy = Actor("Enemy", CombatTeamId.Enemy);
            enemy.transform.SetParent(root.transform, false);
            enemy.transform.position = payload.transform.position;
            Physics.SyncTransforms();
            controller.Tick(1f);

            Assert.That(controller.Snapshot.RouteDistance, Is.EqualTo(10f));
            enemy.GetComponent<ActorHealth>().ApplyDamage(1000f);
            controller.Tick(1f);
            Assert.That(controller.Snapshot.RouteDistance, Is.EqualTo(20f));
        }

        [Test]
        public void WaveStartAndEnemyDeathsAdvanceCheckpointDefense()
        {
            var projectile = new GameObject("Projectile", typeof(CombatProjectile));
            projectile.transform.SetParent(root.transform, false);
            projectile.SetActive(false);
            var anchors = Enumerable.Range(0, 4).Select(index =>
            {
                var anchor = new GameObject($"Anchor {index}").transform;
                anchor.SetParent(root.transform, false);
                return anchor;
            }).ToArray();
            var spawnerObject = new GameObject("Spawner");
            spawnerObject.transform.SetParent(root.transform, false);
            var spawner = spawnerObject.AddComponent<PayloadWaveSpawner>();
            spawner.Configure(player.transform, projectile, null, null, null, null, null, anchors, 55, 18);
            var controller = NewController();
            controller.Configure(true, 55, Bounds(), player.transform, payload.transform, null, spawner, 1000f, 0f, 4.5f);
            player.transform.position = payload.transform.position;
            Physics.SyncTransforms();

            controller.Tick(1f);

            Assert.That(controller.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.CheckpointDefense));
            Assert.That(controller.Snapshot.RemainingEnemyCount, Is.EqualTo(10));
            Assert.That(spawner.ActiveCount, Is.EqualTo(10));

            foreach (var enemy in spawner.LivingEnemies.ToArray())
            {
                enemy.GetComponent<ActorHealth>().ApplyDamage(10000f);
            }

            Assert.That(controller.Snapshot.Phase, Is.EqualTo(PayloadMissionPhase.Escorting));
            Assert.That(controller.Snapshot.NextCheckpointIndex, Is.EqualTo(1));
        }

        [Test]
        public void ReplicaDoesNotTickAndOnlyAppliesSnapshots()
        {
            var controller = NewController();
            controller.Configure(false, 91, Bounds(), player.transform, payload.transform, null, null, 10f, 0f, 4.5f);
            var applied = new PayloadMissionSnapshot(
                PayloadMissionPhase.Escorting, 12f, 20f, 20f / controller.RoutePlan.TotalLength,
                0, -1, 0, 1, 0, true, false);

            controller.Tick(5f);
            Assert.That(controller.Snapshot.RouteDistance, Is.EqualTo(0f));
            Assert.That(controller.ApplySnapshot(applied), Is.True);
            Assert.That(controller.Snapshot, Is.EqualTo(applied));
            Assert.That(payload.transform.position, Is.EqualTo(controller.RoutePlan.EvaluateDistance(20f)));
        }

        [Test]
        public void AuthorityRejectsReplicaSnapshotMutation()
        {
            var controller = NewController();
            controller.Configure(true, 92, Bounds(), player.transform, payload.transform, null, null, 10f, 0f, 4.5f);
            var before = controller.Snapshot;

            Assert.That(controller.ApplySnapshot(new PayloadMissionSnapshot(
                PayloadMissionPhase.Completed, 99f, 99f, 1f, 3, -1, 0, 0, 0, false, false)), Is.False);
            Assert.That(controller.Snapshot, Is.EqualTo(before));
        }

        [Test]
        public void MissingActorDependenciesRemainStationaryWithoutThrowing()
        {
            var controller = NewController();

            Assert.DoesNotThrow(() => controller.Configure(true, 5, Bounds(), null, null, null, null, 10f, 0f, 4.5f));
            Assert.DoesNotThrow(() => controller.Tick(5f));
            Assert.That(controller.Snapshot.RouteDistance, Is.EqualTo(0f));
        }

        [Test]
        public void RouteViewBuildsPolylineAndThreeCheckpointMarkers()
        {
            var route = PayloadRoutePlanner.Generate(8, Bounds(), 5, 3f);
            var viewObject = new GameObject("Route View");
            viewObject.transform.SetParent(root.transform, false);
            var view = viewObject.AddComponent<PayloadRouteView>();

            view.Configure(route, null);

            Assert.That(view.GetComponent<LineRenderer>().positionCount, Is.EqualTo(route.Points.Count));
            Assert.That(view.CheckpointMarkers, Has.Count.EqualTo(4));
        }

        private PayloadMissionController NewController()
        {
            var controllerObject = new GameObject("Mission Controller");
            controllerObject.transform.SetParent(root.transform, false);
            return controllerObject.AddComponent<PayloadMissionController>();
        }

        private static GameObject Actor(string name, CombatTeamId team)
        {
            var actor = new GameObject(name);
            actor.AddComponent<CapsuleCollider>();
            actor.AddComponent<ActorHealth>();
            actor.AddComponent<CombatTeam>().Team = team;
            return actor;
        }

        private static Rect Bounds()
        {
            return new Rect(-28f, -50f, 56f, 100f);
        }
    }
}
