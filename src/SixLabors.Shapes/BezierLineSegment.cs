﻿// <copyright file="BezierLineSegment.cs" company="Scott Williams">
// Copyright (c) Scott Williams and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.Shapes
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Represents a line segment that colonists of control points that will be rendered as a cubic bezier curve
    /// </summary>
    /// <seealso cref="SixLabors.Shapes.ILineSegment" />
    public class BezierLineSegment : ILineSegment
    {
        // code for this taken from <see href="http://devmag.org.za/2011/04/05/bzier-curves-a-tutorial/"/>

        private const float MinimumSqrDistance = 1.75f;
        private const float DivisionThreshold = -0.997f;

        /// <summary>
        /// The line points.
        /// </summary>
        private readonly ImmutableArray<Vector2> linePoints;
        private readonly Vector2[] controlPoints;

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public BezierLineSegment(ImmutableArray<Vector2> points)
        {
            Guard.NotNull(points, nameof(points));
            Guard.MustBeGreaterThanOrEqualTo(points.Length, 4, nameof(points));

            int correctPointCount = (points.Length - 1) % 3;
            if (correctPointCount != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(points), "points must be a multiple of 3 plus 1 long.");
            }

            this.controlPoints = points.ToArray();
            this.linePoints = GetDrawingPoints(points);

            this.EndPoint = points[points.Length - 1];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierLineSegment"/> class.
        /// </summary>
        /// <param name="points">The points.</param>
        public BezierLineSegment(Vector2[] points)
            : this(ImmutableArray.Create(points))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BezierLineSegment"/> class.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="controlPoint1">The control point1.</param>
        /// <param name="controlPoint2">The control point2.</param>
        /// <param name="end">The end.</param>
        /// <param name="additionalPoints">The additional points.</param>
        public BezierLineSegment(Vector2 start, Vector2 controlPoint1, Vector2 controlPoint2, Vector2 end, params Vector2[] additionalPoints)
            : this(new[] { start, controlPoint1, controlPoint2, end }.Merge(additionalPoints))
        {
        }

        /// <summary>
        /// Gets the end point.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public Vector2 EndPoint { get; private set; }

        /// <summary>
        /// Returns the current <see cref="ILineSegment" /> a simple linear path.
        /// </summary>
        /// <returns>
        /// Returns the current <see cref="ILineSegment" /> as simple linear path.
        /// </returns>
        public ImmutableArray<Vector2> Flatten()
        {
            return this.linePoints;
        }

        /// <summary>
        /// Transforms the current LineSegment using specified matrix.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>A line segment with the matrix applied to it.</returns>
        public ILineSegment Transform(Matrix3x2 matrix)
        {
            if (matrix.IsIdentity)
            {
                // no transform to apply skip it
                return this;
            }

            var points = new Vector2[this.controlPoints.Length];
            var i = 0;
            foreach (var p in this.controlPoints)
            {
                points[i++] = Vector2.Transform(p, matrix);
            }

            return new BezierLineSegment(points);
        }

        private static ImmutableArray<Vector2> GetDrawingPoints(ImmutableArray<Vector2> controlPoints)
        {
            List<Vector2> drawingPoints = new List<Vector2>();
            int curveCount = (controlPoints.Length - 1) / 3;

            for (int curveIndex = 0; curveIndex < curveCount; curveIndex++)
            {
                List<Vector2> bezierCurveDrawingPoints = FindDrawingPoints(curveIndex, controlPoints);

                if (curveIndex != 0)
                {
                    //remove the fist point, as it coincides with the last point of the previous Bezier curve.
                    bezierCurveDrawingPoints.RemoveAt(0);
                }

                drawingPoints.AddRange(bezierCurveDrawingPoints);
            }

            return drawingPoints.ToImmutableArray();
        }

        private static List<Vector2> FindDrawingPoints(int curveIndex, ImmutableArray<Vector2> controlPoints)
        {
            List<Vector2> pointList = new List<Vector2>();

            Vector2 left = CalculateBezierPoint(curveIndex, 0, controlPoints);
            Vector2 right = CalculateBezierPoint(curveIndex, 1, controlPoints);

            pointList.Add(left);
            pointList.Add(right);

            FindDrawingPoints(curveIndex, 0, 1, pointList, 1, controlPoints, 0);

            return pointList;
        }

        private static int FindDrawingPoints(
            int curveIndex,
            float t0,
            float t1,
            List<Vector2> pointList,
            int insertionIndex,
            ImmutableArray<Vector2> controlPoints,
            int depth)
        {
            if (depth > 999) // max recursive depth for control points, means this is approx the max number of points discoverable
            {
                return 0;
            }

            Vector2 left = CalculateBezierPoint(curveIndex, t0, controlPoints);
            Vector2 right = CalculateBezierPoint(curveIndex, t1, controlPoints);

            if ((left - right).LengthSquared() < MinimumSqrDistance)
            {
                return 0;
            }

            float midT = (t0 + t1) / 2;
            Vector2 mid = CalculateBezierPoint(curveIndex, midT, controlPoints);

            Vector2 leftDirection = Vector2.Normalize(left - mid);
            Vector2 rightDirection = Vector2.Normalize(right - mid);

            if (Vector2.Dot(leftDirection, rightDirection) > DivisionThreshold || Math.Abs(midT - 0.5f) < 0.0001f)
            {
                int pointsAddedCount = 0;

                pointsAddedCount += FindDrawingPoints(curveIndex, t0, midT, pointList, insertionIndex, controlPoints, depth + 1);
                pointList.Insert(insertionIndex + pointsAddedCount, mid);
                pointsAddedCount++;
                pointsAddedCount += FindDrawingPoints(curveIndex, midT, t1, pointList, insertionIndex + pointsAddedCount, controlPoints, depth + 1);

                return pointsAddedCount;
            }

            return 0;
        }

        private static Vector2 CalculateBezierPoint(int curveIndex, float t, ImmutableArray<Vector2> controlPoints)
        {
            int nodeIndex = curveIndex * 3;

            Vector2 p0 = controlPoints[nodeIndex];
            Vector2 p1 = controlPoints[nodeIndex + 1];
            Vector2 p2 = controlPoints[nodeIndex + 2];
            Vector2 p3 = controlPoints[nodeIndex + 3];

            return CalculateBezierPoint(t, p0, p1, p2, p3);
        }

        /// <summary>
        /// Calculates the bezier point along the line.
        /// </summary>
        /// <param name="t">The position within the line.</param>
        /// <param name="p0">The p 0.</param>
        /// <param name="p1">The p 1.</param>
        /// <param name="p2">The p 2.</param>
        /// <param name="p3">The p 3.</param>
        /// <returns>
        /// The <see cref="Vector2"/>.
        /// </returns>
        private static Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0; // first term

            p += 3 * uu * t * p1; // second term
            p += 3 * u * tt * p2; // third term
            p += ttt * p3; // fourth term

            return p;
        }
    }
}
