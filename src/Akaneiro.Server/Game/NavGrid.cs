using System;
using System.Collections.Generic;
using UnityEngine;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Where monsters can walk, as far as the server knows. The levels carry no navigation data: the
    /// client builds its A* grid from the level geometry when it loads (AstarPath.calculateOnStartup),
    /// and the server does not have that geometry. What the server does know to be walkable is
    /// every place the player has been, and where monsters stand and wander. Monsters chasing the
    /// player therefore keep to those cells: they follow the player's trail around a corner instead
    /// of cutting through the wall, and only cross unknown ground where nothing else leads on.
    /// The client walks the corner points it is sent (NpcBase.SetNextMoveTarget).
    /// </summary>
    public sealed class NavGrid
    {
        /// <summary>Edge of a cell in meters (the client's grids use 1-2 m nodes).</summary>
        public const float CellSize = 1.5f;

        /// <summary>Unknown ground costs this much more than ground someone has walked on.</summary>
        private const int UnknownCost = 8;

        /// <summary>Search limit per path, so a hopeless search cannot stall the server.</summary>
        private const int MaxExpanded = 6000;

        private readonly HashSet<(int X, int Z)> _walked = new HashSet<(int, int)>();
        private Vector3? _last;

        public int KnownCells => _walked.Count;

        public static (int X, int Z) Cell(Vector3 p) => ((int)Math.Floor(p.x / CellSize), (int)Math.Floor(p.z / CellSize));

        private static Vector3 Center((int X, int Z) c, float y) => new Vector3((c.X + 0.5f) * CellSize, y, (c.Z + 0.5f) * CellSize);

        public bool IsKnown(Vector3 p) => _walked.Contains(Cell(p));

        /// <summary>A place someone stands on is walkable.</summary>
        public void MarkPoint(Vector3 p) => _walked.Add(Cell(p));

        /// <summary>The player walked from the last reported place to this one: everything in between is walkable.</summary>
        public void MarkPlayer(Vector3 p)
        {
            if (_last is Vector3 last && SceneInstance.Distance(last, p) < 30f)
                MarkLine(last, p);
            else
                MarkPoint(p);
            _last = p;
        }

        /// <summary>Forgets the trail\'s last point (after a teleport the line in between is not walkable).</summary>
        public void Jumped(Vector3 p)
        {
            MarkPoint(p);
            _last = p;
        }

        private void MarkLine(Vector3 a, Vector3 b)
        {
            foreach (var cell in Line(Cell(a), Cell(b)))
                _walked.Add(cell);
        }

        private bool LineKnown((int X, int Z) a, (int X, int Z) b)
        {
            foreach (var cell in Line(a, b))
            {
                if (!_walked.Contains(cell))
                    return false;
            }
            return true;
        }

        private static IEnumerable<(int X, int Z)> Line((int X, int Z) a, (int X, int Z) b)
        {
            int x = a.X, z = a.Z, dx = Math.Abs(b.X - a.X), dz = Math.Abs(b.Z - a.Z);
            int sx = a.X < b.X ? 1 : -1, sz = a.Z < b.Z ? 1 : -1, err = dx - dz;
            while (true)
            {
                yield return (x, z);
                if (x == b.X && z == b.Z)
                    yield break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }
        }

        /// <summary>
        /// The corner points from <paramref name="from"/> to <paramref name="to"/> (the last one is
        /// <paramref name="to"/>). A straight line where its ground is known or the way is short;
        /// otherwise the cheapest way over known ground, crossing unknown ground only where needed.
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            var start = Cell(from);
            var goal = Cell(to);
            if (start == goal || SceneInstance.Distance(from, to) < 3f || LineKnown(start, goal) || _walked.Count == 0)
                return new List<Vector3> { to };

            // A* over the cells, 8 neighbours, in a box around both ends.
            int margin = 25;
            int minX = Math.Min(start.X, goal.X) - margin, maxX = Math.Max(start.X, goal.X) + margin;
            int minZ = Math.Min(start.Z, goal.Z) - margin, maxZ = Math.Max(start.Z, goal.Z) + margin;

            var open = new PriorityQueue<(int X, int Z), int>();
            var cost = new Dictionary<(int, int), int> { [start] = 0 };
            var came = new Dictionary<(int, int), (int, int)>();
            open.Enqueue(start, 0);
            int expanded = 0;
            bool found = false;

            while (open.TryDequeue(out var cell, out _))
            {
                if (cell == goal)
                {
                    found = true;
                    break;
                }
                if (++expanded > MaxExpanded)
                    break;
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                        continue;
                    var next = (X: cell.X + dx, Z: cell.Z + dz);
                    if (next.X < minX || next.X > maxX || next.Z < minZ || next.Z > maxZ)
                        continue;
                    int step = (dx != 0 && dz != 0 ? 14 : 10) * (_walked.Contains(next) || next == goal ? 1 : UnknownCost);
                    int newCost = cost[cell] + step;
                    if (cost.TryGetValue(next, out int old) && old <= newCost)
                        continue;
                    cost[next] = newCost;
                    came[next] = cell;
                    int h = 10 * Math.Max(Math.Abs(goal.X - next.X), Math.Abs(goal.Z - next.Z));
                    open.Enqueue(next, newCost + h);
                }
            }

            if (!found)
                return new List<Vector3> { to };

            var cells = new List<(int X, int Z)> { goal };
            while (came.TryGetValue(cells[^1], out var previous))
                cells.Add(previous);
            cells.Reverse(); // start ... goal

            // Keep only the corners: skip ahead as long as the straight line stays on known ground.
            var path = new List<Vector3>();
            int at = 0;
            while (at < cells.Count - 1)
            {
                int next = cells.Count - 1;
                while (next > at + 1 && !LineKnown(cells[at], cells[next]))
                    next--;
                if (next < cells.Count - 1)
                    path.Add(Center(cells[next], to.y));
                at = next;
            }
            path.Add(to);
            return path;
        }
    }
}
