namespace SerialPortMacros
{
    public enum SnapGuideKind
    {
        /// <summary>Aligned with an edge or center of another element.</summary>
        Align,
        /// <summary>Aligned with an edge, margin or center line of the panel.</summary>
        Container,
        /// <summary>Same gap as one already used between other elements.</summary>
        Spacing,
        /// <summary>Same width or height as another element.</summary>
        Size
    }

    /// <summary>A guide line to draw, in panel coordinates.</summary>
    public readonly record struct SnapGuide(Point From, Point To, SnapGuideKind Kind);

    /// <summary>
    /// Snapping rules of the interface editor (pure logic, no UI).
    ///
    /// Moving: the left/center/right (and top/middle/bottom) of the element snap to
    ///   - the edges and centers of the other elements,
    ///   - the panel edges, margins and center lines,
    ///   - positions that repeat a gap already used between other elements, or that
    ///     center the element between two neighbours (equal spacing).
    /// Resizing: the right/bottom edge snaps to the same targets, plus the width/height
    ///   of the other elements (match size).
    ///
    /// Each axis is handled by the same code: the vertical axis is processed by
    /// transposing the rectangles (swapping X and Y).
    /// </summary>
    public sealed class SnapEngine
    {
        /// <summary>Maximum distance, in pixels, for a snap.</summary>
        public int Threshold { get; set; } = 8;

        /// <summary>Distance of the margin guides from the panel edges.</summary>
        public int Margin { get; set; } = 10;

        public sealed class Result
        {
            public Rectangle Bounds;
            public List<SnapGuide> Guides = new();
        }

        /// <summary>Snaps a moving element (its size does not change).</summary>
        public Result SnapMove(Rectangle moving, IReadOnlyList<Rectangle> others, Rectangle container)
        {
            var x = SnapAxis(moving, others, container, resize: false);
            var y = Transpose(SnapAxis(T(moving), others.Select(T).ToList(), T(container), resize: false));

            var result = new Result { Bounds = new Rectangle(moving.X + x.Delta, moving.Y + y.Delta, moving.Width, moving.Height) };
            AddGuides(result, x, y);
            return result;
        }

        /// <summary>
        /// Snaps the right/bottom edge of an element being resized (top-left fixed).
        /// The returned deltas are applied by the caller, which may keep the aspect ratio.
        /// </summary>
        public (int Dx, int Dy, List<SnapGuide> Guides) SnapResize(Rectangle proposed, IReadOnlyList<Rectangle> others, Rectangle container)
        {
            var x = SnapAxis(proposed, others, container, resize: true);
            var y = Transpose(SnapAxis(T(proposed), others.Select(T).ToList(), T(container), resize: true));

            var result = new Result();
            AddGuides(result, x, y);
            return (x.Delta, y.Delta, result.Guides);
        }

        /// <summary>Guides of the final rectangle, recomputed after the caller adjusted it.</summary>
        public List<SnapGuide> GuidesFor(Rectangle final, IReadOnlyList<Rectangle> others, Rectangle container, bool resize)
        {
            var saved = Threshold;
            Threshold = 0; // only exact matches
            try
            {
                var x = SnapAxis(final, others, container, resize);
                var y = Transpose(SnapAxis(T(final), others.Select(T).ToList(), T(container), resize));
                var result = new Result();
                AddGuides(result, x, y);
                return result.Guides;
            }
            finally
            {
                Threshold = saved;
            }
        }

        private static void AddGuides(Result result, AxisResult x, AxisResult y)
        {
            result.Guides.AddRange(x.Guides);
            result.Guides.AddRange(y.Guides);
        }

        // ------------------------------------------------------------------
        // One axis (X). Y is handled by transposing.
        // ------------------------------------------------------------------

        private sealed class AxisResult
        {
            public int Delta;
            public List<SnapGuide> Guides = new();
        }

        private readonly record struct Candidate(int Reference, int Target, int Priority, Func<int, IEnumerable<SnapGuide>> Guides);

        private AxisResult SnapAxis(Rectangle m, IReadOnlyList<Rectangle> others, Rectangle c, bool resize)
        {
            var candidates = new List<Candidate>();
            int[] refs = resize ? new[] { m.Right } : new[] { m.Left, Cx(m), m.Right };

            // 1. Edges and centers of the other elements.
            foreach (var o in others)
            {
                foreach (int target in new[] { o.Left, Cx(o), o.Right })
                {
                    foreach (int r in refs)
                    {
                        var other = o;
                        candidates.Add(new Candidate(r, target, 0, delta =>
                        {
                            var moved = Shift(m, delta, resize);
                            int top = Math.Min(moved.Top, other.Top), bottom = Math.Max(moved.Bottom, other.Bottom);
                            return new[] { new SnapGuide(new Point(target, top), new Point(target, bottom), SnapGuideKind.Align) };
                        }));
                    }
                }
            }

            // 2. Panel edges, margins and center line.
            foreach (int target in new[] { c.Left, c.Left + Margin, Cx(c), c.Right - Margin, c.Right })
            {
                foreach (int r in refs)
                {
                    candidates.Add(new Candidate(r, target, 1, _ =>
                        new[] { new SnapGuide(new Point(target, c.Top), new Point(target, c.Bottom), SnapGuideKind.Container) }));
                }
            }

            // 3. Equal spacing with the neighbours on the same row.
            AddSpacingCandidates(m, others, resize, candidates);

            // 4. Same width as another element (resize only).
            if (resize)
            {
                foreach (var o in others)
                {
                    var other = o;
                    candidates.Add(new Candidate(m.Right, m.Left + o.Width, 3, delta =>
                    {
                        var moved = Shift(m, delta, resize);
                        return new[]
                        {
                            new SnapGuide(new Point(other.Left, other.Bottom + 4), new Point(other.Right, other.Bottom + 4), SnapGuideKind.Size),
                            new SnapGuide(new Point(moved.Left, moved.Bottom + 4), new Point(moved.Right, moved.Bottom + 4), SnapGuideKind.Size)
                        };
                    }));
                }
            }

            var result = new AxisResult();

            // Closest candidate within the threshold; on equal distance the lower priority value wins.
            Candidate? best = null;
            foreach (var cand in candidates)
            {
                int dist = Math.Abs(cand.Target - cand.Reference);
                if (dist > Threshold) continue;
                if (best == null)
                {
                    best = cand;
                    continue;
                }
                int bestDist = Math.Abs(best.Value.Target - best.Value.Reference);
                if (dist < bestDist || (dist == bestDist && cand.Priority < best.Value.Priority))
                    best = cand;
            }

            if (best != null)
            {
                result.Delta = best.Value.Target - best.Value.Reference;

                // Show every guide that is exactly matched after the snap (there can be several).
                var seen = new HashSet<SnapGuide>();
                foreach (var cand in candidates)
                {
                    if (cand.Target - cand.Reference != result.Delta) continue;
                    foreach (var g in cand.Guides(result.Delta))
                        if (seen.Add(g))
                            result.Guides.Add(g);
                }
            }

            return result;
        }

        private void AddSpacingCandidates(Rectangle m, IReadOnlyList<Rectangle> others, bool resize, List<Candidate> candidates)
        {
            // Elements on the same row as the moving one.
            var row = others.Where(o => OverlapY(o, m)).ToList();
            if (row.Count == 0) return;

            // Gaps already used between pairs of other elements on a common row
            // (only adjacent pairs: nothing in between).
            var gaps = new List<(int Gap, Rectangle A, Rectangle B)>();
            foreach (var a in others)
            {
                foreach (var b in others)
                {
                    if (a == b || !OverlapY(a, b) || a.Right > b.Left) continue;
                    int gap = b.Left - a.Right;
                    if (gap <= 0) continue;
                    bool blocked = others.Any(o => o != a && o != b && OverlapY(o, a) && OverlapY(o, b) &&
                                                   o.Left >= a.Right && o.Right <= b.Left);
                    if (!blocked)
                        gaps.Add((gap, a, b));
                }
            }

            foreach (var n in row)
            {
                bool nIsLeft = Cx(n) <= Cx(m);

                foreach (var (gap, a, b) in gaps)
                {
                    var ra = a; var rb = b; var neighbour = n;
                    if (nIsLeft && !resize)
                    {
                        // m to the right of n, with the same gap: m.Left = n.Right + gap
                        candidates.Add(new Candidate(m.Left, n.Right + gap, 2, delta =>
                        {
                            var moved = Shift(m, delta, resize);
                            return SpacingGuides(neighbour, moved, ra, rb);
                        }));
                    }
                    else if (!nIsLeft)
                    {
                        // m to the left of n, with the same gap: m.Right = n.Left - gap
                        candidates.Add(new Candidate(m.Right, n.Left - gap, 2, delta =>
                        {
                            var moved = Shift(m, delta, resize);
                            return SpacingGuides(moved, neighbour, ra, rb);
                        }));
                    }
                }
            }

            // Centered between a left and a right neighbour (moving only).
            if (resize) return;
            foreach (var left in row.Where(o => Cx(o) <= Cx(m)))
            {
                foreach (var right in row.Where(o => Cx(o) > Cx(m)))
                {
                    int free = right.Left - left.Right - m.Width;
                    if (free < 0) continue;
                    var l = left; var r = right;
                    candidates.Add(new Candidate(m.Left, left.Right + free / 2, 2, delta =>
                    {
                        var moved = Shift(m, delta, resize);
                        return SpacingGuides(l, moved, moved, r);
                    }));
                }
            }
        }

        /// <summary>Two segments of equal length: the gap a1→a2 and the reference gap b1→b2.</summary>
        private static IEnumerable<SnapGuide> SpacingGuides(Rectangle a1, Rectangle a2, Rectangle b1, Rectangle b2)
        {
            yield return GapSegment(a1, a2);
            yield return GapSegment(b1, b2);
        }

        private static SnapGuide GapSegment(Rectangle left, Rectangle right)
        {
            int top = Math.Max(left.Top, right.Top), bottom = Math.Min(left.Bottom, right.Bottom);
            int y = bottom >= top ? (top + bottom) / 2 : (left.Top + left.Bottom) / 2;
            return new SnapGuide(new Point(left.Right, y), new Point(right.Left, y), SnapGuideKind.Spacing);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static int Cx(Rectangle r) => r.Left + r.Width / 2;

        private static bool OverlapY(Rectangle a, Rectangle b) => a.Top < b.Bottom && b.Top < a.Bottom;

        /// <summary>The rectangle after applying the X delta (moved, or widened when resizing).</summary>
        private static Rectangle Shift(Rectangle r, int delta, bool resize) =>
            resize ? new Rectangle(r.X, r.Y, r.Width + delta, r.Height) : new Rectangle(r.X + delta, r.Y, r.Width, r.Height);

        private static Rectangle T(Rectangle r) => new(r.Y, r.X, r.Height, r.Width);
        private static Point T(Point p) => new(p.Y, p.X);

        private static AxisResult Transpose(AxisResult a)
        {
            for (int i = 0; i < a.Guides.Count; i++)
            {
                var g = a.Guides[i];
                a.Guides[i] = new SnapGuide(T(g.From), T(g.To), g.Kind);
            }
            return a;
        }
    }
}
