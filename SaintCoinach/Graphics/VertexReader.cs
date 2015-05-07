﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaintCoinach.Graphics {
    static class VertexReader {
        public static Vertex Read(byte[] buffer, VertexFormat format, int part1Offset, int part2Offset) {
            Vertex vertex = new Vertex();

            foreach (var element in format.Elements) {
                int elementOffset;
                if (element.SourcePart == 0)
                    elementOffset = part1Offset;
                else if (element.SourcePart == 1)
                    elementOffset = part2Offset;
                else
                    throw new NotSupportedException();

                ReadElement(buffer, element, elementOffset, ref vertex);
            }

            return vertex;
        }
        static void ReadElement(byte[] buffer, VertexFormatElement element, int offset, ref Vertex vertex) {
            var data = ReadData(buffer, element.DataType, offset + element.Offset);

            switch (element.Attribute) {
                case VertexAttribute.BlendIndices:
                    vertex.BlendIndices = (uint)data;
                    break;
                case VertexAttribute.BlendWeights:
                    vertex.BlendWeights = (Vector4)data;
                    break;
                case VertexAttribute.Color:
                    vertex.Color = (Vector4)data;
                    break;
                case VertexAttribute.Normal:
                    vertex.Normal = ForceToVector3(data);
                    break;
                case VertexAttribute.Position:
                    vertex.Position = ForceToVector4(data);
                    break;
                case VertexAttribute.Unknown06:
                    vertex.Unknown06 = (Vector4)data;
                    break;
                case VertexAttribute.UV:
                    vertex.UV = (Vector4)data;
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
        static Vector3 ForceToVector3(object value) {
            if (value is Vector3)
                return (Vector3)value;
            if (value is Vector4) {
                var v4 = (Vector4)value;
                return new Vector3 {
                    X = v4.X,
                    Y = v4.Y,
                    Z = v4.Z
                };
            }
            throw new NotSupportedException();
        }
        static Vector4 ForceToVector4(object value) {
            if (value is Vector4)
                return (Vector4)value;
            if (value is Vector3) {
                var v3 = (Vector3)value;
                return new Vector4 {
                    X = v3.X,
                    Y = v3.Y,
                    Z = v3.Z,
                    W = 1
                };
            }
            throw new NotSupportedException();
        }
        static object ReadData(byte[] buffer, VertexDataType type, int offset) {
            switch (type) {
                case VertexDataType.Half4:
                    return new Vector4 {
                        X = HalfHelper.Unpack(buffer, offset + 0x00),
                        Y = HalfHelper.Unpack(buffer, offset + 0x02),
                        Z = HalfHelper.Unpack(buffer, offset + 0x04),
                        W = HalfHelper.Unpack(buffer, offset + 0x06)
                    };
                case VertexDataType.UInt:
                    return BitConverter.ToUInt32(buffer, offset);
                case VertexDataType.ByteFloat4:
                    return new Vector4 {
                        X = buffer[offset + 0] / 255f,
                        Y = buffer[offset + 1] / 255f,
                        Z = buffer[offset + 2] / 255f,
                        W = buffer[offset + 3] / 255f
                    };
                case VertexDataType.Single3:
                    return buffer.ToStructure<Vector3>(offset);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
