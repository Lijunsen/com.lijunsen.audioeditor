using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    public static class GraphWaveDisplay
    {
        public static int displayID = -1;
        private static int showChannelDisplayNumber = 2;
       // private static float delayTime = 0f;
       /// <summary>
       /// rect的y轴每格包含的时间
       /// </summary>
        public static float TimePerRect = 0.01f;
        /// <summary>
        /// 每个刻度间的时间间隔，从timeScale中取值
        /// </summary>
        private static float timeScalePerAxisLine = 0.1f;
        /// <summary>
        /// 每个显示时间的大刻度内的小刻度数量
        /// </summary>
        private static int subdivisionsLineStep = 10;
        private static readonly List<float> timeScale = new List<float> { 0.1f, 0.5f, 1, 2, 3, 6 };
        private static readonly Color minMaxColor = new Color(1, 0.4f, 0, 1);
        private static readonly Color rmsColor = new Color(1, 0.6f, 0, 1);
        private static readonly Color viewRectBackGroundColor = new Color(0.1803f, 0, 0, 1);
        private static readonly Color axisRectBackGroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
        private static readonly Color axisCommonLineColor = new Color32(0x60, 0x60, 0x60, 0xAA);
        private static readonly Color axisSubdivisionsLineColor = new Color32(0xAA, 0xAA, 0xAA, 0xAA);
        private static Rect scrollViewRect;
        private static Rect viewRect;

        private static bool showMinMax=true;
        private static bool showRMS=true;
        private static bool showBarsAndBeatsAxis = false;
        private static int waveDisplayRectWidth;
        private static float axisRectHeight = 20f;
        private static float[] clipData = new float[0];
        private static List<ChannelDisplay> channelDisplays = new List<ChannelDisplay>();
        private static Material LineMat;
        private static Vector2 scrollviewPositon = Vector2.zero;
        private static GUIStyle myStyle = new GUIStyle();

        private static int bpm = 120;
        private static int  beats = 4;
        private static int bar = 3;

        private static void Initialize(SoundSFX data)
        {
            if (LineMat == null)
                LineMat = EditorGUIUtility.LoadRequired("SceneView/2DHandleLines.mat") as Material;
            if (LineMat == null)
            {
                FieldInfo field = typeof(HandleUtility).GetField("s_HandleWireMaterial2D", BindingFlags.Static | BindingFlags.NonPublic);
                if ((object)field == null)
                    return;
                LineMat = field.GetValue(null) as Material;
            }

            channelDisplays = new List<ChannelDisplay>();

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
            if (LineMat != null && audioClip != null)
            {
                waveDisplayRectWidth = Mathf.CeilToInt(audioClip.length / TimePerRect);
                channelDisplays.Clear();
                showChannelDisplayNumber = audioClip.channels;
                clipData = new float[audioClip.samples * audioClip.channels];
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

                    channelDisplays.Add(new ChannelDisplay(tempData, waveDisplayRectWidth));
                }

                if (channelDisplays[0].cachedDataLength != waveDisplayRectWidth)
                {
                    foreach (var channelDisplay in channelDisplays)
                    {
                        channelDisplay.InitCachedData(waveDisplayRectWidth);
                    }
                }
            }
        }

        public static void Draw(Rect drawRect, SoundSFX data)
        {

            if (LineMat == null)
            {
                Initialize(data);
            }

            if (data.clip!=null)
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
                viewRect.width = (data.delayTime + audioClip.length) / TimePerRect;
                if (viewRect.width < drawRect.width)
                {
                    viewRect.width = drawRect.width;
                }
            }

            scrollviewPositon = GUI.BeginScrollView(scrollViewRect, scrollviewPositon, viewRect, true, false);

            GUI.color = viewRectBackGroundColor;
            GUI.Box(viewRect, string.Empty, GUI.skin.box);
            GUI.color = color;

            GUI.BeginGroup(viewRect);

            var axisDisplayRect = new Rect(0, 0, viewRect.width, axisRectHeight);
            var waveDisplayRect = new Rect(data.delayTime / TimePerRect, axisDisplayRect.height, waveDisplayRectWidth,
                viewRect.height - axisDisplayRect.height - 1);
            GUI.color = axisRectBackGroundColor;
            GUI.Box(axisDisplayRect, string.Empty, GUI.skin.box);
            GUI.color = color;

            if (Event.current.type == EventType.Repaint)
            {
                if (LineMat != null && audioClip != null && channelDisplays.Count != 0)
                {
                    DrawAxis(axisDisplayRect,showBarsAndBeatsAxis);
                    DrawWaveDisplay(waveDisplayRect, showMinMax, showRMS);
                }
            }
            GUI.EndGroup();
            GUI.EndScrollView();
            GUI.color = color;

            var e = Event.current;
            if (drawRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                var preTimePerRect = TimePerRect;
                if (e.delta.y > 0)
                {
                    MouseScrollDown();
                }
                else
                {
                    MouseScrollUp();
                }
                //Debug.Log(scrollviewPositon);
                ReloadData(data);
                var time = (scrollviewPositon.x + e.mousePosition.x) * preTimePerRect;
                var result = time / TimePerRect - e.mousePosition.x < 0 ? 0 : time / TimePerRect -e.mousePosition.x;
                scrollviewPositon.x = result;
                e.Use();
            }

            if (viewRect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && e.shift)
            {
                //Debug.Log("drag");
                data.delayTime += e.delta.x * TimePerRect;
                if (data.delayTime < 0)
                {
                    data.delayTime = 0;
                }
                e.Use();
            }

            if (viewRect.Contains(e.mousePosition) && e.type == EventType.ContextClick)
            {
                GenerateContextClickMenu(drawRect,data);
                e.Use();
            }
        }

        private static void GenerateContextClickMenu(Rect drawRect, SoundSFX data)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Resize"), false, () =>
            {
                Resize(drawRect,data);
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show/MinMax"), false,() =>
            {
                showMinMax = true;
                showRMS = false;
            });
            menu.AddItem(new GUIContent("Show/RMS"), false,() => 
            {
                showRMS = true;
                showMinMax = false;
            });
            menu.AddItem(new GUIContent("Show/Both"),false, () =>
            {
                showRMS = true;
                showMinMax = true;
            } );
            menu.ShowAsContext();
        }

        private static void Resize(Rect drawRect , SoundSFX data)
        {
            if (Math.Abs(drawRect.width) <= 1 || Math.Abs(drawRect.height) <= 1) return;
            if (data.clip != null)
            {
                //Debug.Log("WaveDisplay Resize");
                if ((data.delayTime + data.clip.length) / TimePerRect > drawRect.width)
                {
                    var index = 0;
                    while ((data.delayTime + data.clip.length) / TimePerRect > drawRect.width)
                    {
                        MouseScrollDown();
                        index++;
                        if (index == 100) break;
                    }
                }
                else if ((data.delayTime + data.clip.length) / TimePerRect < drawRect.width)
                {
                    var index = 0;
                    while ((data.delayTime + data.clip.length) / TimePerRect < drawRect.width)
                    {
                        MouseScrollUp();
                        index++;
                        if (index == 100) break;
                    }
                    MouseScrollDown();
                }
                ReloadData(data);
            }
        }

        /// <summary>
        /// 缩小坐标轴
        /// </summary>
        private static void MouseScrollDown()
        {
            var e = Mathf.FloorToInt(Mathf.Log10(TimePerRect));
            TimePerRect += 5 * Mathf.Pow(10, e - 1);
            if (timeScalePerAxisLine * subdivisionsLineStep / TimePerRect < 40)
            {
                var i = timeScale.FindIndex((x) => x == timeScalePerAxisLine);
                if (i == timeScale.Count - 1)
                {
                    TimePerRect -= 5 * Mathf.Pow(10, e - 1);
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
        private static void MouseScrollUp()
        {
            var e = Mathf.FloorToInt(Mathf.Log10(TimePerRect));
            var digits = Mathf.RoundToInt(TimePerRect / Mathf.Pow(10, e));

            if (digits == 1)
            {
                TimePerRect -= 5 * Mathf.Pow(10, e - 2);
            }
            else
            {
                TimePerRect -= 5 * Mathf.Pow(10, e - 1);
            }
            var i = timeScale.FindIndex((x) => x == timeScalePerAxisLine);
            if (i > 0)
            {
                if (timeScale[i - 1] * subdivisionsLineStep / TimePerRect >= 40)
                {
                    i--;
                    timeScalePerAxisLine = timeScale[i];
                }
            }
        }
        private static void DrawAxis(Rect axisRect,bool DrawBarsAndBeatsAixs)
        {
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(axisRect.x,axisRectHeight),new Vector3(axisRect.x+axisRect.width,axisRectHeight) );
            List<int> drawtimeLabelPosition = new List<int>();
            if (DrawBarsAndBeatsAixs)
            {
                //ShowBarsAndBeatsAixs
                Debug.LogError("Beats轴仍未完成");
                subdivisionsLineStep = bar;
                float timeperBeats =(float) 60 / bpm / beats * 4;
                LineMat.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                float time = 0;
                int subdivisionsLineIndex = 0;
                for (int i = 0; i < axisRect.width; i++)
                {
                    time += TimePerRect;
                    if (time >= timeperBeats)
                    {
                        subdivisionsLineIndex++;
                        if (subdivisionsLineIndex == subdivisionsLineStep)
                        {
                            GL.Color(axisSubdivisionsLineColor);
                            GL.Vertex(new Vector3(i, 0));
                            GL.Vertex(new Vector3(i, axisRect.height / 2));
                            GL.Vertex(new Vector3(i, axisRect.height));
                            GL.Vertex(new Vector3(i, viewRect.height - 1));
                            subdivisionsLineIndex = 0;
                            drawtimeLabelPosition.Add(i);
                        }
                        else
                        {
                            GL.Color(axisCommonLineColor);
                            GL.Vertex(new Vector3(i, 0));
                            GL.Vertex(new Vector3(i, axisRect.height / 4));
                            GL.Vertex(new Vector3(i, axisRect.height));
                            GL.Vertex(new Vector3(i, viewRect.height - 1));
                        }
                        time -= timeperBeats;
                    }
                }
                GL.End();
                GL.PopMatrix();
            }
            else
            {
                //ShowTimelineAxis
                LineMat.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                float time = 0;
                int subdivisionsLineIndex = 0;
                for (int i = 0; i < axisRect.width; i++)
                {
                    time += TimePerRect;
                    if (time >= timeScalePerAxisLine)
                    {
                        subdivisionsLineIndex++;
                        if (subdivisionsLineIndex == subdivisionsLineStep)
                        {
                            GL.Color(axisSubdivisionsLineColor);
                            GL.Vertex(new Vector3(i, 0));
                            GL.Vertex(new Vector3(i, axisRect.height / 2));
                            GL.Vertex(new Vector3(i, axisRect.height));
                            GL.Vertex(new Vector3(i, viewRect.height - 1));
                            subdivisionsLineIndex = 0;
                            drawtimeLabelPosition.Add(i);
                        }
                        else
                        {
                            GL.Color(axisCommonLineColor);
                            GL.Vertex(new Vector3(i, 0));
                            GL.Vertex(new Vector3(i, axisRect.height / 4));
                            GL.Vertex(new Vector3(i, axisRect.height));
                            GL.Vertex(new Vector3(i, viewRect.height - 1));
                        }
                        time -= timeScalePerAxisLine;
                    }
                }
                GL.End();
                GL.PopMatrix();

                foreach (var i in drawtimeLabelPosition)
                {
                    var min = Mathf.FloorToInt(i * TimePerRect / 60);
                    var second = i * TimePerRect % 60;
                    if (Mathf.RoundToInt(second) == 29)
                    {
                        second += 1;
                    }
                    if (Mathf.CeilToInt(second) == 60)
                    {
                        min += 1;
                        second = 0;
                    }
                    EditorGUI.LabelField(new Rect(i, axisRectHeight / 2 - 2, 15, axisRectHeight / 2), string.Format("{0:00}:{1:00}", min, second), myStyle);
                }
            }
        }

        private static void DrawWaveDisplay(Rect waveDisplayRect, bool drawMinMax, bool drawRMS)
        {
            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y), new Vector3(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height));
            Handles.DrawLine(new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y + waveDisplayRect.height), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y));
            Handles.color = Color.black;
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y));
            Handles.DrawLine(new Vector3(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height), new Vector3(waveDisplayRect.x + waveDisplayRect.width - 1, waveDisplayRect.y + waveDisplayRect.height));

            //TODO: 改为使用AudioCurveRandering来绘制时间轴上更精确的波形图
            LineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < showChannelDisplayNumber; i++)
            {
                //Rect channelRect = new Rect(viewRect.x + delayTime / TimePerRect, viewRect.y + viewRect.height / showChannelDisplayNumber * i, viewRect.width - delayTime / TimePerRect, viewRect.height / showChannelDisplayNumber);
                Rect channelRect = new Rect(waveDisplayRect.x, waveDisplayRect.y + waveDisplayRect.height / showChannelDisplayNumber * i, waveDisplayRect.width, waveDisplayRect.height / showChannelDisplayNumber);
        
                GL.Color(drawRMS ? rmsColor : minMaxColor);
                GL.Vertex(new Vector3(channelRect.x, channelRect.center.y));
                GL.Vertex(new Vector3(channelRect.x + channelRect.width, channelRect.center.y));

                int maxHeight = (int)(channelRect.height/2);
                Vector3 startPoint = Vector3.zero;
                Vector3 endPoint = Vector3.zero;
                float y = channelRect.center.y;
                float x = channelRect.x;
                float num3 = 1f / EditorGUIUtility.pixelsPerPoint;
                for (float num4 = 0.0f; (double)num4 < 1.0; num4 += num3)
                {
                    int num5 = 0;
                    for (int index = 0; index < Mathf.CeilToInt(channelRect.width); ++index)
                    {
                        WaveformCacheEntry waveformCacheEntry = channelDisplays[i] == null ? channelDisplays[0].cachedData[index] : channelDisplays[i].cachedData[index];
                        startPoint.x = x + num5;
                        endPoint.x = startPoint.x;
                        if (drawMinMax)
                        {
                            GL.Color(minMaxColor);
                            startPoint.y = y - waveformCacheEntry.max * maxHeight;
                            endPoint.y = y - waveformCacheEntry.min * maxHeight;
                            GL.Vertex(startPoint);
                            GL.Vertex(endPoint);
                        }

                        if (drawRMS)
                        {
                            GL.Color(rmsColor);
                            startPoint.y = y - waveformCacheEntry.rms * maxHeight;
                            endPoint.y = y + waveformCacheEntry.rms * maxHeight;
                            GL.Vertex(startPoint);
                            GL.Vertex(endPoint);
                        }
                        num5++;
                    }
                    x += num3;
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        [Obsolete]
        private static void DrawRMS()
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
            LineMat = null;
            clipData = null;
            channelDisplays.Clear();
            channelDisplays = null;
            AudioEditorView.DestroyWindow -= OnDestroy;
        }


        internal class ChannelDisplay
        {
            private float[] sampleData = new float[0];
            public int cachedDataLength;
            public WaveformCacheEntry[] cachedData;

            /// <summary>
            /// 储存根据sample数据计算获得的图表信息
            /// </summary>
            /// <param name="inSamples">AudioClip的sample数据</param>
            /// <param name="length">Rect的长度</param>
            public ChannelDisplay(float[] inSamples, int length)
            {
                sampleData = inSamples;
                InitCachedData(length);

            }

            public void InitCachedData(int length)
            {
                cachedDataLength = length;
                cachedData = new WaveformCacheEntry[cachedDataLength];
                CalculateCachedData();
            }

            private void CalculateCachedData()
            {
                var samplesPerPack = sampleData.Length / cachedData.Length;
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
                sampleData = null;
                for (int i = 0; i < cachedData.Length; i++)
                {
                    cachedData[i].max = 0;
                    cachedData[i].min = 0;
                    cachedData[i].rms = 0;
                }
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
