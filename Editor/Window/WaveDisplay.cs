using AudioEditor.Runtime.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor
{
    internal static class GraphWaveDisplay
    {
        public static int displayID = -1;

        /// <summary>
        /// rect的y轴每格包含的sample数据量
        /// </summary>
        private static int sampleDataNumberPerRect = 1;

        /// <summary>
        /// rect的y轴每格包含的时间
        /// </summary>
        public static float timePerRect = 0.01f;

        /// <summary>
        /// 每个刻度间的时间间隔，从timeScale中取值
        /// </summary>
        private static float timeScalePerAxisLine = 0.1f;

        // /// <summary>
        // /// 每个显示时间的大刻度内的小刻度数量
        // /// </summary>
        // private static int subdivisionsLineStep = 10;

        private static readonly List<float> timeScale = new List<float> {0.001f, 0.01f, 0.1f, 0.5f, 1, 2, 3, 6 };
        private static readonly Color minMaxColor = new Color(1, 0.4f, 0, 1);
        private static readonly Color rmsColor = new Color(1, 0.6f, 0, 1);
        private static readonly Color viewRectBackGroundColor = new Color(0.1803f, 0, 0, 1);
        private static readonly Color axisRectBackGroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
        private static readonly Color axisCommonLineColor = new Color32(0x60, 0x60, 0x60, 0xFF);
        private static readonly Color axisSubdivisionsLineColor = new Color32(0xAA, 0xAA, 0xAA, 0xFF);
        private static Rect scrollViewRect;
        private static Rect viewRect;

        private static bool showMinMax = true;
        private static bool showRms = true;
        private static bool showBarsAndBeatsAxis = false;
        private static int waveDisplayRectWidth;
        private static float axisRectHeight = 20f;

        //private static float[] clipData = new float[0];

        //private static List<ChannelDisplay> channelDisplays = new List<ChannelDisplay>();

        private static WaveClipBuffer currentClipBuffer;
        private static List<WaveClipBuffer> waveClipBuffers = new List<WaveClipBuffer>();

        private static Material lineMat;
        private static Vector2 scrollviewPosition = Vector2.zero;
        private static GUIStyle myStyle = new GUIStyle();
        private const int TimeLabelWith = 55;

        private static void Initialize(SoundSFX data)
        {
            if (lineMat == null)
                lineMat = EditorGUIUtility.LoadRequired("SceneView/2DHandleLines.mat") as Material;
            if (lineMat == null)
            {
                FieldInfo field = typeof(HandleUtility).GetField("s_HandleWireMaterial2D", BindingFlags.Static | BindingFlags.NonPublic);
                if ((object)field == null)
                    return;
                lineMat = field.GetValue(null) as Material;
            }

            waveClipBuffers = new List<WaveClipBuffer>();

            myStyle = new GUIStyle();
            myStyle.normal.textColor = axisSubdivisionsLineColor;
            myStyle.alignment = TextAnchor.UpperLeft;
            myStyle.margin = new RectOffset(2, 0, 0, 1);
            myStyle.fontSize = (int)axisRectHeight / 2;
            myStyle.fontStyle = FontStyle.Bold;

            AudioEditorView.DestroyWindow -= OnDestroy;
            AudioEditorView.DestroyWindow += OnDestroy;
            //  ReloadData(data);
        }

        private static void ReloadData(SoundSFX data)
        {
            //生成多个Channel的数据
            var audioClip = data.clip;
            if (lineMat != null && audioClip != null)
            {
                //需要对timePerRect和width进行进一步修正，以确保离散是数据正好分布在rect的每一格y轴中
                waveDisplayRectWidth = Mathf.RoundToInt(audioClip.length / timePerRect);
                if (waveDisplayRectWidth != 0)
                {
                    sampleDataNumberPerRect = audioClip.samples / waveDisplayRectWidth;

                    if (sampleDataNumberPerRect < 1)
                    {
                        sampleDataNumberPerRect = 1;
                    }
                    timePerRect = audioClip.length / audioClip.samples * sampleDataNumberPerRect;

                    waveDisplayRectWidth = audioClip.samples / sampleDataNumberPerRect;
                }
                

                if (waveClipBuffers.Exists(x => x.id == data.id))
                {
                    currentClipBuffer = waveClipBuffers.Find(x => x.id == data.id);
                }
                else
                {
                    var newClipBuffer = WaveClipBuffer.Creat(data);
                    waveClipBuffers.Add(newClipBuffer);
                    currentClipBuffer = newClipBuffer;
                }

                // if (currentClipBuffer == null)
                // {
                //     Debug.Log("currentClipBuffer == null");
                // }
                // else
                // {
                //     Debug.Log("select buffer:"+currentClipBuffer.id);
                // }

                if (currentClipBuffer !=null && currentClipBuffer.channelDisplays[0].cachedDataLength != waveDisplayRectWidth)
                {
                    foreach (var channelDisplay in currentClipBuffer.channelDisplays)
                    {
                        //Task.Run(() =>
                        //{
                        //    channelDisplay.InitCachedData((int)waveDisplayRectWidth);
                        //});
                        channelDisplay.InitCachedData((int)waveDisplayRectWidth);
                    }
                }


            }
        }

        public static void Draw(Rect drawRect, SoundSFX data)
        {

            if (lineMat == null)
            {
                Initialize(data);
            }

            if (data.clip != null)
            {
                if (data.clip.loadState == AudioDataLoadState.Unloaded &&
                    data.clip.loadType == AudioClipLoadType.DecompressOnLoad)
                {
                    data.clip.LoadAudioData();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    //加载完成或者加载方式不为DecompressOnLoad时（GetData()数据为空）直接Resize显示
                    if (data.clip.loadState == AudioDataLoadState.Loaded ||
                        data.clip.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        if (data.id != displayID)
                        {
                            Resize(drawRect, data);
                            displayID = data.id;
                        }
                    }
                }
            }

            var color = GUI.color;
            var audioClip = data.clip;
            scrollViewRect = drawRect;

            viewRect = scrollViewRect;
            viewRect.height -= GUI.skin.horizontalScrollbar.fixedHeight;
            if (audioClip != null)
            {
                viewRect.width = (data.delayTime + audioClip.length) / timePerRect;
                if (viewRect.width < drawRect.width)
                {
                    viewRect.width = drawRect.width;
                }
            }

            scrollviewPosition = GUI.BeginScrollView(scrollViewRect, scrollviewPosition, viewRect, true, false);

            GUI.color = viewRectBackGroundColor;
            GUI.Box(viewRect, string.Empty, GUI.skin.box);
            GUI.color = color;

            GUI.BeginGroup(viewRect);

            var axisDisplayRect = new Rect(0, 0, viewRect.width, axisRectHeight);
            var waveDisplayRect = new Rect(data.delayTime / timePerRect, axisDisplayRect.height, waveDisplayRectWidth,
                viewRect.height - axisDisplayRect.height - 1);
            GUI.color = axisRectBackGroundColor;
            GUI.Box(axisDisplayRect, string.Empty, GUI.skin.box);
            GUI.color = color;

            if (Event.current.type == EventType.Repaint)
            {
                if (lineMat != null && audioClip != null)
                {
                    DrawAxis(axisDisplayRect, showBarsAndBeatsAxis);
                    DrawWaveDisplay(waveDisplayRect, showMinMax, showRms);
                }
            }
            GUI.EndGroup();
            GUI.EndScrollView();
            GUI.color = color;

            var e = Event.current;
            if (drawRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                var preTimePerRect = timePerRect;
                if (e.delta.y > 0)
                {
                    MouseScrollDown(data.clip);
                }
                else
                {
                    MouseScrollUp(data.clip);
                }

                //Debug.Log(scrollviewPositon);
                ReloadData(data);
                var time = (scrollviewPosition.x + e.mousePosition.x) * preTimePerRect;
                var result = time / timePerRect - e.mousePosition.x < 0 ? 0 : time / timePerRect - e.mousePosition.x;
                scrollviewPosition.x = result;
                e.Use();
            }

            if (viewRect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && e.shift)
            {
                //Debug.Log("drag");
                data.delayTime += e.delta.x * timePerRect;
                if (data.delayTime < 0)
                {
                    data.delayTime = 0;
                }
                e.Use();
            }

            if (viewRect.Contains(e.mousePosition) && e.type == EventType.ContextClick)
            {
                GenerateContextClickMenu(drawRect, data);
                e.Use();
            }
        }

        private static void GenerateContextClickMenu(Rect drawRect, SoundSFX data)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Resize"), false, () =>
            {
                Resize(drawRect, data);
            });
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Show/MinMax"), showMinMax && !showRms, () =>
             {
                 showMinMax = true;
                 showRms = false;
             });
            menu.AddItem(new GUIContent("Show/RMS"), !showMinMax && showRms, () =>
            {
                showRms = true;
                showMinMax = false;
            });
            menu.AddItem(new GUIContent("Show/Both"), showMinMax && showRms, () =>
             {
                 showRms = true;
                 showMinMax = true;
             });
            menu.ShowAsContext();
        }

        private static void Resize(Rect drawRect, SoundSFX data)
        {
            if (Math.Abs(drawRect.width) <= 1 || Math.Abs(drawRect.height) <= 1) return;
            if (data.clip != null)
            {
                //Debug.Log("WaveDisplay Resize");
                if ((data.delayTime + data.clip.length) / timePerRect > drawRect.width)
                {
                    var index = 0;
                    while ((data.delayTime + data.clip.length) / timePerRect > drawRect.width)
                    {
                        MouseScrollDown(data.clip);
                        index++;
                        if (index == 100) break;
                    }
                }
                else if ((data.delayTime + data.clip.length) / timePerRect < drawRect.width)
                {
                    var index = 0;
                    while ((data.delayTime + data.clip.length) / timePerRect < drawRect.width)
                    {
                        MouseScrollUp(data.clip);
                        index++;
                        if (index == 100) break;
                    }
                    MouseScrollDown(data.clip);
                }
                ReloadData(data);
            }
        }

        /// <summary>
        /// 缩小坐标轴
        /// </summary>
        private static void MouseScrollDown(AudioClip audioClip)
        {
            var e = Mathf.FloorToInt(Mathf.Log10(timePerRect));

            if (audioClip != null)
            {

                var newSampleDataNumberPerRect = 1;
                var index = 0;
                do
                {

                    timePerRect += 5 * Mathf.Pow(10, e - 1);

                    var newWaveDisplayRectWidth = Mathf.RoundToInt(audioClip.length / timePerRect);

                    if (newWaveDisplayRectWidth != 0)
                    {
                        newSampleDataNumberPerRect = audioClip.samples / newWaveDisplayRectWidth;
                    }
                    else
                    {
                        break;
                    }

                    index++;
                    if (index > 100)
                    {
                        AudioEditorDebugLog.LogWarning("越界");
                        break;
                    }
                } while (newSampleDataNumberPerRect == sampleDataNumberPerRect);

            }
            else
            {
                timePerRect += 5 * Mathf.Pow(10, e - 1);
            }

            if (timeScalePerAxisLine * 10 / timePerRect < TimeLabelWith)
            {
                var i = timeScale.FindIndex((x) => x == timeScalePerAxisLine);
                if (i == timeScale.Count - 1)
                {
                    timePerRect -= 5 * Mathf.Pow(10, e - 1);
                }
                if (i + 1 < timeScale.Count)
                {
                    i++;
                    timeScalePerAxisLine = timeScale[i];
                }
            }
        }

        /// <summary>
        /// 放大坐标轴
        /// </summary>
        private static void MouseScrollUp(AudioClip audioClip)
        {
            var e = Mathf.FloorToInt(Mathf.Log10(timePerRect));
            var digits = Mathf.RoundToInt(timePerRect / Mathf.Pow(10, e));

            if (audioClip != null)
            {
                if (sampleDataNumberPerRect != 1)
                {
                    var newSampleDataNumberPerRect = 1;
                    var index = 0;
                    do
                    {
                        if (digits == 1)
                        {
                            timePerRect -= 5 * Mathf.Pow(10, e - 2);
                        }
                        else
                        {
                            timePerRect -= 5 * Mathf.Pow(10, e - 1);
                        }

                        var newWaveDisplayRectWidth = Mathf.RoundToInt(audioClip.length / timePerRect);

                        if (newWaveDisplayRectWidth != 0)
                        {
                            newSampleDataNumberPerRect = audioClip.samples / newWaveDisplayRectWidth;
                            if (newSampleDataNumberPerRect < 1) newSampleDataNumberPerRect = 1;
                        }
                        else
                        {
                            break;
                        }

                        index++;
                        if (index > 100)
                        {
                            AudioEditorDebugLog.LogWarning("越界");
                            break;
                        }
                    } while (newSampleDataNumberPerRect == sampleDataNumberPerRect);
                }
            }
            else
            {

                if (digits == 1)
                {
                    timePerRect -= 5 * Mathf.Pow(10, e - 2);
                }
                else
                {
                    timePerRect -= 5 * Mathf.Pow(10, e - 1);
                }
            }

            var i = timeScale.FindIndex((x) => x == timeScalePerAxisLine);
            if (i > 0)
            {
                if (timeScale[i - 1] * 10 / timePerRect >= TimeLabelWith)
                {
                    i--;
                    timeScalePerAxisLine = timeScale[i];
                }
            }
        }

        private static void DrawAxis(Rect axisRect, bool drawBarsAndBeatsAixs)
        {
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(axisRect.x, axisRectHeight), new Vector3(axisRect.x + axisRect.width, axisRectHeight));
            List<int> drawtimeLabelPosition = new List<int>();
            if (drawBarsAndBeatsAixs)
            {
                //ShowBarsAndBeatsAixs
                Debug.LogError("Beats轴仍未完成");
                // subdivisionsLineStep = bar;
                // float timeperBeats = (float)60 / bpm / beats * 4;
                // lineMat.SetPass(0);
                // GL.PushMatrix();
                // GL.Begin(GL.LINES);
                // float time = 0;
                // int subdivisionsLineIndex = 0;
                // for (int i = 0; i < axisRect.width; i++)
                // {
                //     time += timePerRect;
                //     if (time >= timeperBeats)
                //     {
                //         subdivisionsLineIndex++;
                //         if (subdivisionsLineIndex == subdivisionsLineStep)
                //         {
                //             GL.Color(axisSubdivisionsLineColor);
                //             GL.Vertex(new Vector3(i, 0));
                //             GL.Vertex(new Vector3(i, axisRect.height / 2));
                //             GL.Vertex(new Vector3(i, axisRect.height));
                //             GL.Vertex(new Vector3(i, viewRect.height - 1));
                //             subdivisionsLineIndex = 0;
                //             drawtimeLabelPosition.Add(i);
                //         }
                //         else
                //         {
                //             GL.Color(axisCommonLineColor);
                //             GL.Vertex(new Vector3(i, 0));
                //             GL.Vertex(new Vector3(i, axisRect.height / 4));
                //             GL.Vertex(new Vector3(i, axisRect.height));
                //             GL.Vertex(new Vector3(i, viewRect.height - 1));
                //         }
                //         time -= timeperBeats;
                //     }
                // }
                // GL.End();
                // GL.PopMatrix();
            }
            else
            {
                //ShowTimelineAxis

                // lineMat.SetPass(0);
                // GL.PushMatrix();
                // GL.Begin(GL.LINES);
                // float time = 0;
                // int subdivisionsLineIndex = 0;
                // for (int i = 0; i < axisRect.width; i++)
                // {
                //     time += timePerRect;
                //     if (time >= timeScalePerAxisLine)
                //     {
                //         subdivisionsLineIndex++;
                //         if (subdivisionsLineIndex == subdivisionsLineStep)
                //         {
                //             GL.Color(axisSubdivisionsLineColor);
                //             GL.Vertex(new Vector3(i, 0));
                //             GL.Vertex(new Vector3(i, axisRect.height / 2));
                //             GL.Vertex(new Vector3(i, axisRect.height));
                //             GL.Vertex(new Vector3(i, viewRect.height - 1));
                //             subdivisionsLineIndex = 0;
                //             drawtimeLabelPosition.Add(i);
                //         }
                //         else
                //         {
                //             GL.Color(axisCommonLineColor);
                //             GL.Vertex(new Vector3(i, 0));
                //             GL.Vertex(new Vector3(i, axisRect.height / 4));
                //             GL.Vertex(new Vector3(i, axisRect.height));
                //             GL.Vertex(new Vector3(i, viewRect.height - 1));
                //         }
                //         time -= timeScalePerAxisLine;
                //     }
                // }
                // GL.End();
                // GL.PopMatrix();
                //
                // var labelwith = false;
                // if (drawtimeLabelPosition.Count >= 2)
                // {
                //     if (drawtimeLabelPosition[1] - drawtimeLabelPosition[0] > 55)
                //     {
                //         labelwith = true;
                //     }
                // }
                //
                // if (drawtimeLabelPosition.Count == 1)
                // {
                //     labelwith = true;
                // }
                //
                // foreach (var i in drawtimeLabelPosition)
                // {
                //     var min = Mathf.FloorToInt(i * timePerRect / 60);
                //     var second = i * timePerRect % 60;
                //
                //     // if (Mathf.RoundToInt(second) == 29)
                //     // {
                //     //     second += 1;
                //     // }
                //
                //     if (Mathf.CeilToInt(second) == 60)
                //     {
                //         min += 1;
                //         second = 0;
                //     }
                //
                //     var rect = new Rect(i, axisRectHeight / 2 - 2, 55, axisRectHeight / 2);
                //     if (labelwith)
                //     {
                //         EditorGUI.LabelField(rect, string.Format("{0:00}:{1:00}:{2:000}", min, (int)second, (second - (int)second) * 1000f), myStyle);
                //     }
                //     else
                //     {
                //         EditorGUI.LabelField(rect, string.Format("{0:00}:{1:00}", min, (int)second), myStyle);    
                //     }
                // }

                // lineMat.SetPass(0);
                // GL.PushMatrix();
                // GL.Begin(GL.LINES);

                var timeRectPosition = 0f;
                List<float> timeLinePositionList = new List<float>();
                var j = 0;
                while (timeRectPosition < axisRect.width)
                {
                    timeRectPosition = j * timeScalePerAxisLine * 10 / timePerRect ;
                    timeLinePositionList.Add(timeRectPosition);

                    j++;
                    if (j == 1000)
                    {
                        break;
                    }
                }

                var labelwith = false;
                if (timeLinePositionList.Count >= 2)
                {
                    if (timeLinePositionList[1] - timeLinePositionList[0] > TimeLabelWith && timeScalePerAxisLine <= timeScale[1])
                    {
                        labelwith = true;
                    }
                }

                if (timeLinePositionList.Count == 1)
                {
                    labelwith = true;
                }

                var point1 = new Vector2(0, 0);
                var point2 = new Vector2(0, axisRect.height / 2);
                var point3 = new Vector2(0, axisRect.height);
                var point4 = new Vector2(0, viewRect.height - 1);

                for (var i = 0; i < timeLinePositionList.Count; i++)
                {

                    var time = i * timeScalePerAxisLine * 10;
                    var min = Mathf.FloorToInt(time / 60);
                    var second = (time - min * 60 );
                    var millimeter = (second - (int) second) * 1000f;

                    var rect = new Rect(timeLinePositionList[i], axisRectHeight / 2 - 2, TimeLabelWith, axisRectHeight / 2);
                    if (labelwith)
                    {
                        EditorGUI.LabelField(rect, $"{min:00}:{(int)second:00}:{millimeter:000}",
                            myStyle);
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, $"{min:00}:{(int)second:00}", myStyle);
                    }

                    point1.x = point2.x = point3.x = point4.x = timeLinePositionList[i];

                    var check = false;
                    //对于小于等于0.1秒内的刻度，重点线的跨度是100（每10条线画一个重点线）
                    if (timeScalePerAxisLine <= 0.1f)
                    {
                        var isAxisSubdivisionsLine = Math.Abs((time / timeScalePerAxisLine) % 100);
                        if (isAxisSubdivisionsLine < 0.001f || Math.Abs(isAxisSubdivisionsLine - 100) < 0.001f)
                            check = true;
                    }
                    //对于大于等于0.1秒的刻度，重点线的跨度是60倍（每6条线画一个重点线）
                    if (timeScalePerAxisLine >= 0.5f)
                    {
                        var isAxisSubdivisionsLine = Math.Abs((time / timeScalePerAxisLine) % 60);
                        if (isAxisSubdivisionsLine < 0.001f || Math.Abs(isAxisSubdivisionsLine - 60) < 0.001f)
                            check = true;
                    }
                    if (check)
                    {
                        point2.y = axisRect.height / 2;
                        Handles.color = axisSubdivisionsLineColor;
                    }
                    else
                    {
                        point2.y = axisRect.height / 4;
                        Handles.color = axisCommonLineColor; ;
                    }
                    Handles.DrawLine(point1, point2);
                    Handles.DrawLine(point3, point4);
                }
            }
        }

        private static void DrawWaveDisplay(Rect waveDisplayRect, bool drawMinMax, bool drawRms)
        {
            //左右框线为白，上下框线为黑
            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y), new Vector3(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height));
            Handles.DrawLine(new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y + waveDisplayRect.height), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y));
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y));
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y + waveDisplayRect.height));


            //LineMat.SetPass(0);
            //GL.PushMatrix();
            //GL.Begin(GL.LINES);
            //for (int i = 0; i < showChannelDisplayNumber; i++)
            //{
            //    //Rect channelRect = new Rect(viewRect.x + delayTime / TimePerRect, viewRect.y + viewRect.height / showChannelDisplayNumber * i, viewRect.width - delayTime / TimePerRect, viewRect.height / showChannelDisplayNumber);
            //    Rect channelRect = new Rect(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height / showChannelDisplayNumber * i, waveDisplayRect.width, waveDisplayRect.height / showChannelDisplayNumber);

            //    GL.Color(drawRMS ? rmsColor : minMaxColor);
            //    GL.Vertex(new Vector3(channelRect.x, channelRect.center.y));
            //    GL.Vertex(new Vector3(channelRect.x + channelRect.width, channelRect.center.y));

            //    int maxHeight = (int)(channelRect.height / 2);
            //    Vector3 startPoint = Vector3.zero;
            //    Vector3 endPoint = Vector3.zero;
            //    float y = channelRect.center.y;
            //    float x = channelRect.x;
            //    float num3 = 1f / EditorGUIUtility.pixelsPerPoint;
            //    for (float num4 = 0.0f; (double)num4 < 1.0; num4 += num3)
            //    {
            //        int num5 = 0;
            //        for (int index = 0; index < Mathf.CeilToInt(channelRect.width); ++index)
            //        {
            //            WaveformCacheEntry waveformCacheEntry = channelDisplays[i] == null ? channelDisplays[0].cachedData[index] : channelDisplays[i].cachedData[index];
            //            startPoint.x = x + num5;
            //            endPoint.x = startPoint.x;
            //            if (drawMinMax)
            //            {
            //                GL.Color(minMaxColor);
            //                startPoint.y = y - waveformCacheEntry.max * maxHeight;
            //                endPoint.y = y - waveformCacheEntry.min * maxHeight;
            //                GL.Vertex(startPoint);
            //                GL.Vertex(endPoint);
            //            }

            //            if (drawRMS)
            //            {
            //                GL.Color(rmsColor);
            //                startPoint.y = y - waveformCacheEntry.rms * maxHeight;
            //                endPoint.y = y + waveformCacheEntry.rms * maxHeight;
            //                GL.Vertex(startPoint);
            //                GL.Vertex(endPoint);
            //            }
            //            num5++;
            //        }
            //        x += num3;
            //    }
            //}
            //GL.End();
            //GL.PopMatrix();

            if (currentClipBuffer == null)
            {
                //Debug.Log("null");
                return;
            }

            for (int i = 0; i < currentClipBuffer.channelDisplays.Count; i++)
            {
                //Rect channelRect = new Rect(viewRect.x + delayTime / TimePerRect, viewRect.y + viewRect.height / showChannelDisplayNumber * i, viewRect.width - delayTime / TimePerRect, viewRect.height / showChannelDisplayNumber);
                if(currentClipBuffer ==null)
                    return;

                Rect channelRect = new Rect(waveDisplayRect.x,
                    waveDisplayRect.y + waveDisplayRect.height / currentClipBuffer.channelDisplays.Count * i, waveDisplayRect.width,
                    waveDisplayRect.height / currentClipBuffer.channelDisplays.Count);

                var channelDisplay = currentClipBuffer.channelDisplays[i] == null ? currentClipBuffer.channelDisplays[0] : currentClipBuffer.channelDisplays[i];

                if (channelRect.width > 1)
                {
                    if (drawMinMax)
                    {
                        var index = -1;
                        AudioCurveRendering.DrawMinMaxFilledCurve(channelRect, ((float f, out Color col, out float minValue,
                            out float maxValue) =>
                        {
                            col = minMaxColor;
                            var preIndex = index;
                            index = Mathf.Clamp(Mathf.RoundToInt(f * channelDisplay.cachedData.Length), 0,
                                channelDisplay.cachedData.Length - 1);

                            WaveformCacheEntry waveformCacheEntry = channelDisplay.cachedData[index];
                            minValue = waveformCacheEntry.min;
                            maxValue = waveformCacheEntry.max;

                            if (preIndex > 0 && index > preIndex+1)
                            {
                                for (int j = preIndex; j < index; j++)
                                {
                                    if (minValue > channelDisplay.cachedData[j].min)
                                    {
                                        minValue = channelDisplay.cachedData[j].min;
                                    }

                                    if (maxValue < channelDisplay.cachedData[j].max)
                                    {
                                        maxValue = channelDisplay.cachedData[j].max;
                                    }
                                }
                            }
                        }));

                        // for (int j = 0; j < waveDisplayRectWidth; j++)
                        // {
                        //     Handles.color = minMaxColor;
                        //
                        //     WaveformCacheEntry waveformCacheEntry = channelDisplay.cachedData[Mathf.Clamp(j, 0,
                        //         channelDisplay.cachedData.Length - 1)];
                        //
                        //     Handles.DrawLine(new Vector3(channelRect.x + j, channelRect.y + waveformCacheEntry.min* channelRect.height), new Vector3(channelRect.x + j, channelRect.y + waveformCacheEntry.max * channelRect.height));
                        //
                        // }
                    }

                    if (drawRms)
                    {
                        var index = -1;
                           AudioCurveRendering.DrawMinMaxFilledCurve(channelRect, ((float f, out Color col, out float minValue,
                            out float maxValue) =>
                        {
                            col = rmsColor;
                            var preIndex = index;
                            index = Mathf.Clamp(Mathf.RoundToInt(f * channelDisplay.cachedData.Length), 0,
                                channelDisplay.cachedData.Length - 1);
                            WaveformCacheEntry waveformCacheEntry = channelDisplay.cachedData[index];

                            maxValue = waveformCacheEntry.rms;
                            minValue = - waveformCacheEntry.rms;


                            if (preIndex > 0 && index > preIndex + 1)
                            {
                                for (int j = preIndex; j < index; j++)
                                {
                                    if (maxValue < channelDisplay.cachedData[j].rms)
                                    {
                                        maxValue = channelDisplay.cachedData[j].rms;
                                        minValue = -maxValue;
                                    }
                                }
                            }
                        }));
                    }
                }

            }
        }

        [Obsolete]
        private static void DrawRms()
        {
            //GL.Color(rmsColor);
            //for (int i = 0; i < showChannelDisplayNumber; i++)
            //{
            //    Rect channelRect = new Rect(delayTime / TimePerRect, viewRect.height / showChannelDisplayNumber * i, viewRect.width - delayTime / TimePerRect, viewRect.height / showChannelDisplayNumber);
            //    GL.Vertex(new Vector3(channelRect.x, channelRect.center.y));
            //    GL.Vertex(new Vector3(channelRect.x + channelRect.width, channelRect.center.y));

            //    int maxHeight = (int)(1 * channelRect.height);
            //    Vector3 startPoint = Vector3.zero;
            //    Vector3 endPoint = Vector3.zero;
            //    float y = channelRect.center.y;
            //    float x = channelRect.x;
            //    float num3 = 1f / EditorGUIUtility.pixelsPerPoint;
            //    for (float num4 = 0.0f; (double)num4 < 1.0; num4 += num3)
            //    {
            //        int num5 = 0;
            //        for (int index = 0; index < Mathf.CeilToInt(channelRect.width); ++index)
            //        {
            //            WaveformCacheEntry waveformCacheEntry = channelDisplays[i] == null ? channelDisplays[0].cachedData[index] : channelDisplays[i].cachedData[index];
            //            startPoint.x = x + num5;
            //            endPoint.x = startPoint.x;
            //            startPoint.y = y - waveformCacheEntry.rms * maxHeight;
            //            endPoint.y = y + waveformCacheEntry.rms * maxHeight;
            //            GL.Vertex(startPoint);
            //            GL.Vertex(endPoint);
            //            num5++;
            //        }
            //        x += num3;
            //    }
            //}

        }

        private static void OnDestroy()
        {
            displayID = -1;
            lineMat = null;
            //channelDisplays.Clear();
            //channelDisplays = null;
            AudioEditorView.DestroyWindow -= OnDestroy;
            currentClipBuffer = null;
            for (int i = 0; i < waveClipBuffers.Count;i++ )
            {
                WaveClipBuffer.SafeRelease(waveClipBuffers[i]);
            }
            waveClipBuffers.Clear();
            waveClipBuffers = null;
        }

        internal class WaveClipBuffer:ScriptableObject
        {
            public int id;
            public List<ChannelDisplay> channelDisplays;

            public static WaveClipBuffer Creat(SoundSFX data)
            {
                var waveClipBuffer = ScriptableObject.CreateInstance<WaveClipBuffer>();
                waveClipBuffer.name = data.name;
                waveClipBuffer.id = data.id;
                waveClipBuffer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontUnloadUnusedAsset;
                waveClipBuffer.Init(data);
                return waveClipBuffer;
            }

            public void Init(SoundSFX data)
            {
                var audioClip = data.clip;
                if (audioClip != null)
                {
                    channelDisplays = new List<ChannelDisplay>();

                    //TODO 有时samples会大于getData的长度
                    var clipData = new float[audioClip.samples * audioClip.channels];
                    if (audioClip.loadType == AudioClipLoadType.DecompressOnLoad)
                    {
                        audioClip.GetData(clipData, 0);
                    }

                    for (int i = 0; i < audioClip.channels; i++)
                    {
                        float[] tempData = new float[audioClip.samples];
                        for (int j = 0; j < tempData.Length; j++)
                        {
                            tempData[j] = clipData[i + j * audioClip.channels];
                        }
                    
                        var initCacheData = audioClip.loadType == AudioClipLoadType.DecompressOnLoad;
                        channelDisplays.Add(new ChannelDisplay(tempData, (int)waveDisplayRectWidth, initCacheData));
                    }
                    
                    //if (channelDisplays[0].cachedDataLength != waveDisplayRectWidth)
                    //{
                    //    foreach (var channelDisplay in channelDisplays)
                    //    {
                    //        channelDisplay.InitCachedData(waveDisplayRectWidth);
                    //    }
                    //}
                }
            }

            public void Release()
            {
                foreach (var channelDisplay in channelDisplays)
                {
                    channelDisplay.Release();
                }

                channelDisplays.Clear();
                channelDisplays = null;

            }

            public static void SafeRelease(WaveClipBuffer waveClipBuffer)
            {
                if(waveClipBuffer == null) return;
                waveClipBuffer.Release();
                DestroyImmediate(waveClipBuffer);
            }
        }

        [System.Serializable]
        internal class ChannelDisplay
        {
            public float[] sampleData = new float[0];
            public int cachedDataLength;
            public WaveformCacheEntry[] cachedData;

            /// <summary>
            /// 储存根据sample数据计算获得的图表信息
            /// </summary>
            /// <param name="inSamples">AudioClip的sample数据</param>
            /// <param name="length">Rect的长度</param>
            /// <param name="initCachData">是否直接初始化数据，false时将返回全为0的数据</param>
            public ChannelDisplay(float[] inSamples, int length,bool initCachData = true)
            {
                sampleData = inSamples;
                InitCachedData(length, initCachData);
            }
            
            public void InitCachedData(int length, bool initCachData = true)
            {
                cachedDataLength = length;
                cachedData = new WaveformCacheEntry[cachedDataLength];
                if (initCachData)
                {
                    CalculateCachedData();
                }
            }

            private void CalculateCachedData()
            {
                var samplesPerPack = sampleData.Length / cachedDataLength;
                //Debug.Log("_samplesPerPack" + samplesPerPack);
                //Debug.Log("with" + cachedData.Length);
                for (int i = 0; i < cachedData.Length; i++)
                {
                    float sumPerPack = 0.0f;
                    float a1 = 1f;
                    float a2 = -1f;
                    for (int j = i * samplesPerPack; j < (i + 1) * samplesPerPack; j++)
                    {
                        float b = sampleData[j];
                        sumPerPack += b * b;
                        a1 = Mathf.Min(a1, b);
                        a2 = Mathf.Max(a2, b);
                    }

                    cachedData[i].rms = Mathf.Sqrt(sumPerPack / samplesPerPack);
                    cachedData[i].min = a1;
                    cachedData[i].max = a2;

                }
            }

            public void ClearData()
            {
                sampleData = new float[0];
                for (int i = 0; i < cachedData.Length; i++)
                {
                    cachedData[i].max = 0;
                    cachedData[i].min = 0;
                    cachedData[i].rms = 0;
                }
            }

            public void Release()
            {
                //ClearData();
                sampleData = null;
                cachedData = null;
            }
        }

        internal struct WaveformCacheEntry
        {
            public float rms;
            public float min;
            public float max;
        }
    }
}
