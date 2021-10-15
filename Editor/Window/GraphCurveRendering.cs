using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor
{
    internal static class GraphCurveRendering
    {
        //      private static float GraphPadding = 5f;
        private const float FontHeight = 16f;

        private const float FontWith = 28f;

        //public static Material LineMat;
        private static GUIStyle myStyle;
        private static readonly float[] axisScale = { 0.1f, 0.25f, 0.5f };
        private static readonly int maxSubdivisionsAxis = 10;

        private static readonly Color backgroundColor = new Color(0.19215f, 0.19215f, 0.19215f);
        private static void Initialize()
        {
            AudioEditorView.DestroyWindow -= OnDestroy;
            AudioEditorView.DestroyWindow += OnDestroy;

            myStyle = new GUIStyle();
            myStyle.normal.textColor = Color.white;
            myStyle.alignment = TextAnchor.LowerCenter;
            myStyle.stretchWidth = true;
            // MyStyle.margin = new RectOffset(0, 0, 5, 5);

            // if (LineMat == null)
            //     LineMat = EditorGUIUtility.LoadRequired("SceneView/2DHandleLines.mat") as Material;
            // if (LineMat != null)
            //     return;
            // FieldInfo field = typeof(HandleUtility).GetField("s_HandleWireMaterial2D", BindingFlags.Static | BindingFlags.NonPublic);
            // if ((object)field == null)
            //     return;
            // LineMat = field.GetValue(null) as Material;
        }

        /// <summary>
        /// 绘制一个与animationCurve相关的坐标轴图表
        /// </summary>
        /// <param name="rect">控件的位置</param>
        /// <param name="animationCurve">需要绘制的animationCurve</param>
        /// <param name="xAxisMin">x轴最小值</param>
        /// <param name="xAxisMax">x轴最大值</param>
        /// <param name="yAxisMin">y轴最小值</param>
        /// <param name="yAxisMax">y轴最大值</param>
        /// <returns>在控件中具体图表的Rect</returns>
        public static Rect Draw(Rect rect, List<AnimationCurve> animationCurve, float? xAxisMin = null, float? xAxisMax = null, float? yAxisMin = null, float? yAxisMax = null, float? xAxisValue = null)
        {
            EditorGUI.DrawRect(rect, backgroundColor);
            if (myStyle == null) Initialize();
            // if (LineMat == null) Initialize();
            var dataRect = new Rect(FontWith, FontHeight - 1, rect.width - FontWith * 2, rect.height - FontHeight * 2);
            GUI.BeginGroup(rect);
            DrawGraphData(dataRect, animationCurve);
            DrawCoordinateSystem(rect, dataRect, xAxisMin, xAxisMax, yAxisMin, yAxisMax);
            DrawXAxisValueRect(dataRect, xAxisMin, xAxisMax, xAxisValue);
            GUI.EndGroup();

            return new Rect(rect.x + dataRect.x, rect.y + dataRect.y, dataRect.width, dataRect.height);
        }

        /// <summary>
        /// 绘制一个与animationCurve相关的坐标轴图表
        /// </summary>
        /// <param name="rect">控件的位置</param>
        /// <param name="animationCurve">需要绘制的animationCurve</param>
        /// <param name="curveColor">对应animationCurve的颜色</param>
        /// <param name="xAxisMin">x轴最小值</param>
        /// <param name="xAxisMax">x轴最大值</param>
        /// <param name="yAxisMin">y轴最小值</param>
        /// <param name="yAxisMax">y轴最大值</param>
        /// <returns>在控件中具体图表的Rect</returns>
        public static Rect Draw(Rect rect, AnimationCurve animationCurve, Color curveColor, float? xAxisMin = null, float? xAxisMax = null, float? yAxisMin = null, float? yAxisMax = null, float? xAxisValue = null)
        {
            EditorGUI.DrawRect(rect, backgroundColor);
            if (myStyle == null) Initialize();
            // if (LineMat == null) Initialize();
            var dataRect = new Rect(FontWith, FontHeight - 1, rect.width - FontWith * 2, rect.height - FontHeight * 2);
            GUI.BeginGroup(rect);
            DrawGraphData(dataRect, animationCurve, curveColor);
            DrawCoordinateSystem(rect, dataRect, xAxisMin, xAxisMax, yAxisMin, yAxisMax);
            DrawXAxisValueRect(dataRect, xAxisMin, xAxisMax, xAxisValue);
            GUI.EndGroup();

            return new Rect(rect.x + dataRect.x, rect.y + dataRect.y, dataRect.width, dataRect.height);
        }

        /// <summary>
        /// 绘制坐标轴
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="xAxisMin"></param>
        /// <param name="xAxisMax"></param>
        /// <param name="yAxisMin"></param>
        /// <param name="yAxisMax"></param>
        /// <param name="xAxisValue"></param>
        private static void DrawCoordinateSystem(Rect rect, Rect graphDataRect, float? xAxisMin, float? xAxisMax, float? yAxisMin, float? yAxisMax)
        {
            Vector3 zeroPoint = new Vector3(FontWith, rect.height - FontHeight);
            Vector3 upperPoint = new Vector3(FontWith, FontHeight);
            Vector3 rightPoint = new Vector3(rect.width - FontWith, rect.height - FontHeight);
            // Vector3 rightUpPotint = new Vector3(rect.width - FontWith, FontHeight);

            //绘制坐标轴线
            // LineMat.SetPass(0);
            // GL.PushMatrix();
            // GL.Begin(GL.LINES);
            // GL.Color(Color.white);
            // GL.Vertex(upperPoint);
            // GL.Vertex(zeroPoint);
            // GL.Vertex(zeroPoint);
            // GL.Vertex(rightPoint);
            // GL.End();
            // GL.PopMatrix();

            Handles.color = Color.white;
            Handles.DrawLine(zeroPoint, upperPoint);
            Handles.DrawLine(zeroPoint, rightPoint);

            //绘制x轴两个端点
            var label = xAxisMin == null ? "" : xAxisMin.ToString();
            DrawCalibration(graphDataRect, false, zeroPoint, label);
            label = xAxisMax == null ? "" : xAxisMax.ToString();
            DrawCalibration(graphDataRect, false, rightPoint, label);
            //根据差值算法绘制中间的x轴坐标
            if (xAxisMin != null && xAxisMax != null)
            {
                var xDvalue = (float)(xAxisMax - xAxisMin);
                var e = Mathf.FloorToInt(Mathf.Log10(xDvalue));
                //差值的位数
                var figures = Mathf.Pow(10, e);
                var subdivisionsAxisNumber = 0;
                int step = 0;
                for (int i = 0; i < 10; i++)
                {
                    var result = xDvalue / (axisScale[step] * figures);
                    if (result <= maxSubdivisionsAxis)
                    {
                        subdivisionsAxisNumber = Mathf.FloorToInt(result);
                        break;
                    }
                    step++;
                    if (step == axisScale.Length)
                    {
                        step = 0;
                        figures *= 10;
                    }
                }

                for (int i = 0; i < subdivisionsAxisNumber; i++)
                {
                    //如果与端点值太近则不绘制
                    if (i == subdivisionsAxisNumber - 1)
                    {
                        if (Mathf.Abs((float)(xAxisMin + axisScale[step] * figures * (i + 1) - xAxisMax)) < 0.5)
                        {
                            break;
                        }
                    }
                    var point = zeroPoint;
                    point.x += graphDataRect.width * (axisScale[step] * figures * (i + 1) / xDvalue);
                    var valueLabel = (float)xAxisMin + axisScale[step] * figures * (i + 1);
                    DrawCalibration(graphDataRect, false, point, valueLabel.ToString("G5"));
                }
            }
            //绘制y轴的两个端点
            label = yAxisMin == null ? "" : yAxisMin.ToString();
            DrawCalibration(graphDataRect, true, zeroPoint, label);
            label = yAxisMax == null ? "" : ((float)yAxisMax).ToString("G");
            DrawCalibration(graphDataRect, true, upperPoint, label);

            //TODO 如果坐标太过密集应该选择隔位显示
            //绘制y轴中间的端点
            if (yAxisMin != null && yAxisMax != null)
            {
                var yDvalue = (float)(yAxisMax - yAxisMin);
                var e = Mathf.FloorToInt(Mathf.Log10(yDvalue));
                //差值的位数
                var figures = Mathf.Pow(10, e);
                var subdivisionsAxisNumber = 0;
                int step = 0;
                for (int i = 0; i < 10; i++)
                {
                    var result = yDvalue / (axisScale[step] * figures);
                    if (result <= maxSubdivisionsAxis)
                    {
                        subdivisionsAxisNumber = Mathf.FloorToInt(result);
                        break;
                    }
                    step++;
                    if (step == axisScale.Length)
                    {
                        step = 0;
                        figures *= 10;
                    }
                }

                for (int i = 0; i < subdivisionsAxisNumber; i++)
                {
                    if (i == subdivisionsAxisNumber - 1)
                    {
                        if (Mathf.Abs((float)(yAxisMin + axisScale[step] * figures * (i + 1) - yAxisMax)) < 0.5)
                        {
                            break;
                        }
                    }
                    var point = zeroPoint;
                    point.y -= graphDataRect.height * (axisScale[step] * figures * (i + 1) / yDvalue);
                    var valueLabel = (float)yAxisMin + axisScale[step] * figures * (i + 1);
                    var labelStyle = "G";
                    switch (step)
                    {
                        case 0:
                        case 3:
                            labelStyle = "F1";
                            break;
                        case 1:
                            labelStyle = "F2";
                            break;
                    }
                    DrawCalibration(graphDataRect, true, point, valueLabel.ToString(labelStyle));
                }
            }
        }

        /// <summary>
        /// 绘制带虚线刻度的坐标
        /// </summary>
        /// <param name="dataRect"></param>
        /// <param name="isLeft"></param>
        /// <param name="point"></param>
        /// <param name="label"></param>
        private static void DrawCalibration(Rect dataRect, bool isLeft, Vector3 point, string label)
        {
            if (isLeft)
            {
                var startPoint = new Vector3(point.x, point.y);
                var rightPoint = startPoint;
                var leftPoint = startPoint;
                leftPoint.x += 3;
                rightPoint.x += dataRect.width;

                // LineMat.SetPass(0);
                // GL.PushMatrix();
                // GL.Begin(GL.LINE_STRIP);
                // GL.Color(new Color(1, 1, 1, 0.2f));
                //
                // GL.Vertex(startPoint);
                // GL.Vertex(rightPoint);
                //
                // GL.End();
                // GL.Begin(GL.LINES);
                // GL.Color(Color.white);
                // GL.Vertex(startPoint);
                // GL.Vertex(leftPoint);
                //
                // GL.End();
                // GL.PopMatrix();
                Handles.color = Color.white;
                Handles.DrawLine(startPoint, leftPoint);
                Handles.color = new Color(1, 1, 1, 0.2f);
                Handles.DrawLine(startPoint, rightPoint);

                var labelRect = new Rect(point.x - FontWith - 3, point.y - FontHeight / 2, FontWith, FontHeight);
                myStyle.alignment = TextAnchor.MiddleRight;
                myStyle.fontSize = 11;
                EditorGUI.LabelField(labelRect, label, myStyle);
            }
            else
            {
                var startPoint = new Vector3(point.x, point.y);
                var upPoint = startPoint;
                var bottomPoint = startPoint;
                bottomPoint.y += 3;
                upPoint.y -= dataRect.height;

                // LineMat.SetPass(0);
                // GL.PushMatrix();
                // GL.Begin(GL.LINE_STRIP);
                // GL.Color(new Color(1, 1, 1, 0.2f));
                //
                // GL.Vertex(startPoint);
                // GL.Vertex(upPoint);
                //
                // GL.End();
                // GL.Begin(GL.LINES);
                // GL.Color(Color.white);
                // GL.Vertex(startPoint);
                // GL.Vertex(bottomPoint);
                //
                // GL.End();
                // GL.PopMatrix();

                Handles.color = Color.white;
                Handles.DrawLine(startPoint, bottomPoint);
                Handles.color = new Color(1, 1, 1, 0.2f);
                Handles.DrawLine(startPoint, upPoint);

                myStyle.alignment = TextAnchor.LowerCenter;
                myStyle.fontSize = 0;
                var labelRect = new Rect(point.x - FontWith / 2, point.y, FontWith, FontHeight);
                EditorGUI.LabelField(labelRect, label, myStyle);
            }

        }

        /// <summary>
        /// 绘制多个曲线
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="animationCurve"></param>
        private static void DrawGraphData(Rect rect, List<AnimationCurve> animationCurve)
        {
            AudioCurveRendering.BeginCurveFrame(rect);
            if (animationCurve == null || animationCurve.Count == 0)
            {
                AudioCurveRendering.EndCurveFrame();
                return;
            }
            for (int i = 0; i < animationCurve.Count; i++)
            {
                var curveData = animationCurve[i];
                //float minValue = float.MaxValue;
                //float maxValue = float.MinValue;
                //for (float j = 0; j < 1; j += 0.05f)
                //{
                //    minValue =Mathf.Clamp01(Mathf.Min(minValue, curveData.Evaluate(j))) ;
                //    maxValue =Mathf.Clamp01(Mathf.Max(maxValue, curveData.Evaluate(j)));
                //}

                //var medianValue = (maxValue - minValue) / 2;

                AudioCurveRendering.DrawCurve(new Rect(0, 0, rect.width, rect.height), (f =>
                    {
                        var value = Mathf.Clamp01(curveData.Evaluate(f));
                        var normalizedValue = (value - 0.5f) / 0.5f;
                        return normalizedValue * 0.99f;
                    }), AudioEditorView.colorRankList[i]);
            }
            AudioCurveRendering.EndCurveFrame();
        }
        /// <summary>
        /// 绘制单个曲线
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="animationCurve"></param>
        /// <param name="color"></param>
        private static void DrawGraphData(Rect rect, AnimationCurve animationCurve, Color color)
        {
            AudioCurveRendering.BeginCurveFrame(rect);
            if (animationCurve == null)
            {
                AudioCurveRendering.EndCurveFrame();
                return;
            }

            AudioCurveRendering.DrawCurve(new Rect(0, 0, rect.width, rect.height), (f =>
            {
                var value = Mathf.Clamp01(animationCurve.Evaluate(f));
                var normalizedValue = (value - 0.5f) / 0.5f;
                return normalizedValue * 0.99f;
            }), color);
            AudioCurveRendering.EndCurveFrame();
        }

        /// <summary>
        /// 绘制X轴当前值的坐标显示
        /// </summary>
        private static void DrawXAxisValueRect(Rect graphRect, float? xMin, float? xMax, float? xAxisValue)
        {
            if (xMin != null && xMax != null && xAxisValue != null)
            {
                var xAxisValueRect = graphRect;
                var rectCenterValue = (float)(graphRect.x + graphRect.width * ((xAxisValue - xMin) / (xMax - xMin)));
                xAxisValueRect.xMin = rectCenterValue - 1;
                xAxisValueRect.xMax = rectCenterValue + 1;

                var preColor = GUI.color;
                GUI.color = Color.yellow;
                GUI.DrawTexture(xAxisValueRect, EditorGUIUtility.whiteTexture);
                GUI.color = preColor;
            }
        }

        private static void OnDestroy()
        {
            //LineMat = null;
            myStyle = null;
            AudioEditorView.DestroyWindow -= OnDestroy;
        }
    }
}
