﻿using System.Collections.Generic;
using System.Numerics;
using Windows.Data.Json;
using LottieUWP.Utils;

namespace LottieUWP.Model.Content
{
    internal class ShapeData
    {
        private readonly List<CubicCurveData> _curves = new List<CubicCurveData>();
        private Vector2 _initialPoint;
        private bool _closed;

        private ShapeData(Vector2 initialPoint, bool closed, List<CubicCurveData> curves)
        {
            _initialPoint = initialPoint;
            _closed = closed;
            _curves.AddRange(curves);
        }

        internal ShapeData()
        {
        }

        private void SetInitialPoint(float x, float y)
        {
            if (_initialPoint == null)
            {
                _initialPoint = new Vector2();
            }
            _initialPoint.X = x;
            _initialPoint.Y = y;
        }

        internal virtual Vector2 InitialPoint => _initialPoint;

        internal virtual bool Closed => _closed;

        internal virtual List<CubicCurveData> Curves => _curves;

        internal virtual void InterpolateBetween(ShapeData shapeData1, ShapeData shapeData2, float percentage)
        {
            if (_initialPoint == null)
            {
                _initialPoint = new Vector2();
            }
            _closed = shapeData1.Closed || shapeData2.Closed;

            if (_curves.Count > 0 && _curves.Count != shapeData1.Curves.Count && _curves.Count != shapeData2.Curves.Count)
            {
                throw new System.InvalidOperationException("Curves must have the same number of control points. This: " + Curves.Count + "\tShape 1: " + shapeData1.Curves.Count + "\tShape 2: " + shapeData2.Curves.Count);
            }
            if (_curves.Count == 0)
            {
                for (var i = shapeData1.Curves.Count - 1; i >= 0; i--)
                {
                    _curves.Add(new CubicCurveData());
                }
            }

            var initialPoint1 = shapeData1.InitialPoint;
            var initialPoint2 = shapeData2.InitialPoint;


            SetInitialPoint(MiscUtils.Lerp(initialPoint1.X, initialPoint2.X, percentage), MiscUtils.Lerp(initialPoint1.Y, initialPoint2.Y, percentage));

            for (var i = _curves.Count - 1; i >= 0; i--)
            {
                var curve1 = shapeData1.Curves[i];
                var curve2 = shapeData2.Curves[i];

                var cp11 = curve1.ControlPoint1;
                var cp21 = curve1.ControlPoint2;
                var vertex1 = curve1.Vertex;

                var cp12 = curve2.ControlPoint1;
                var cp22 = curve2.ControlPoint2;
                var vertex2 = curve2.Vertex;

                _curves[i].SetControlPoint1(MiscUtils.Lerp(cp11.X, cp12.X, percentage), MiscUtils.Lerp(cp11.Y, cp12.Y, percentage));
                _curves[i].SetControlPoint2(MiscUtils.Lerp(cp21.X, cp22.X, percentage), MiscUtils.Lerp(cp21.Y, cp22.Y, percentage));
                _curves[i].SetVertex(MiscUtils.Lerp(vertex1.X, vertex2.X, percentage), MiscUtils.Lerp(vertex1.Y, vertex2.Y, percentage));
            }
        }

        public override string ToString()
        {
            return "ShapeData{" + "numCurves=" + _curves.Count + "closed=" + _closed + '}';
        }

        internal class Factory : IAnimatableValueFactory<ShapeData>
        {
            internal static readonly Factory Instance = new Factory();

            public ShapeData ValueFromObject(IJsonValue @object, float scale)
            {
                JsonObject pointsData = null;
                if (@object.ValueType == JsonValueType.Array)
                {
                    var firstObject = @object.GetArray()[0];
                    if (firstObject.ValueType == JsonValueType.Object && firstObject.GetObject().ContainsKey("v"))
                    {
                        pointsData = firstObject.GetObject();
                    }
                }
                else if (@object.ValueType == JsonValueType.Object && @object.GetObject().ContainsKey("v"))
                {
                    pointsData = @object.GetObject();
                }

                if (pointsData == null)
                {
                    return null;
                }

                var pointsArray = pointsData.GetNamedArray("v", null);
                var inTangents = pointsData.GetNamedArray("i", null);
                var outTangents = pointsData.GetNamedArray("o", null);
                var closed = pointsData.GetNamedBoolean("c", false);

                if (pointsArray == null || inTangents == null || outTangents == null || pointsArray.Count != inTangents.Count || pointsArray.Count != outTangents.Count)
                {
                    throw new System.InvalidOperationException("Unable to process points array or tangents. " + pointsData);
                }
                if (pointsArray.Count == 0)
                {
                    return new ShapeData(new Vector2(), false, new List<CubicCurveData>());
                }

                var length = pointsArray.Count;
                var vertex = VertexAtIndex(0, pointsArray);
                vertex.X *= scale;
                vertex.Y *= scale;
                var initialPoint = vertex;
                var curves = new List<CubicCurveData>(length);

                for (var i = 1; i < length; i++)
                {
                    vertex = VertexAtIndex(i, pointsArray);
                    var previousVertex = VertexAtIndex(i - 1, pointsArray);
                    var cp1 = VertexAtIndex(i - 1, outTangents);
                    var cp2 = VertexAtIndex(i, inTangents);
                    var shapeCp1 = previousVertex + cp1;
                    var shapeCp2 = vertex + cp2;

                    shapeCp1.X *= scale;
                    shapeCp1.Y *= scale;
                    shapeCp2.X *= scale;
                    shapeCp2.Y *= scale;
                    vertex.X *= scale;
                    vertex.Y *= scale;

                    curves.Add(new CubicCurveData(shapeCp1, shapeCp2, vertex));
                }

                if (closed)
                {
                    vertex = VertexAtIndex(0, pointsArray);
                    var previousVertex = VertexAtIndex(length - 1, pointsArray);
                    var cp1 = VertexAtIndex(length - 1, outTangents);
                    var cp2 = VertexAtIndex(0, inTangents);

                    var shapeCp1 = previousVertex + cp1;
                    var shapeCp2 = vertex + cp2;

                    if (scale != 1f)
                    {
                        shapeCp1.X *= scale;
                        shapeCp1.Y *= scale;
                        shapeCp2.X *= scale;
                        shapeCp2.Y *= scale;
                        vertex.X *= scale;
                        vertex.Y *= scale;
                    }

                    curves.Add(new CubicCurveData(shapeCp1, shapeCp2, vertex));
                }
                return new ShapeData(initialPoint, closed, curves);
            }

            internal static Vector2 VertexAtIndex(int idx, JsonArray points)
            {
                if (idx >= points.Count)
                {
                    throw new System.ArgumentException("Invalid index " + idx + ". There are only " + points.Count + " points.");
                }

                var pointArray = points.GetArrayAt((uint)idx);
                var x = pointArray[0];
                var y = pointArray[1];
                return new Vector2(x != null ? (float)x.GetNumber() : 0, y != null ? (float)y.GetNumber() : 0);
            }
        }
    }
}