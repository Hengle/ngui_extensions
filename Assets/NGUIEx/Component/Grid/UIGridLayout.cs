//----------------------------------------------
// NGUI extensions
// License: The MIT License ( http://opensource.org/licenses/MIT )
// Copyright © 2013-2018 mulova@gmail.com
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Object = UnityEngine.Object;

using commons;
using comunity;

namespace ngui.ex
{
    [ExecuteInEditMode]
    [AddComponentMenu("NGUI/Ex/GridLayout")]
    public class UIGridLayout : UILayout, IEnumerable<Transform>
    {
        public enum HAlign
        {
            Left,
            Center,
            Right,
            None
        }

        public enum VAlign
        {
            Top,
            Center,
            Bottom,
            None
        }

        public enum Arrangement
        {
            Horizontal,
            Vertical,
        }

        public const string ROW_SELECTION_METHOD = "OnRowSelected";
		
        public Arrangement arrangement = Arrangement.Horizontal;
        public HAlign halign = HAlign.None;
        public VAlign valign = VAlign.None;
        public HAlign[] haligns = new HAlign[0];
        public VAlign[] valigns = new VAlign[0];
        public int maxPerLine = 1;
        public Vector2 padding;
        public Vector2 cellSize;
        // cell minimum size.
        public Vector2 cellMinSize;
        // cell minimum size
        public int totalWidth;
        // table size
        public Transform[] components = new Transform[0];
        public int rowHeader;
        public int columnHeader;

        public Color gizmoColor = new Color(1f, 0f, 0f);
        private Bounds[,] bounds;
		
        public GameObject[] rowPrefab = new GameObject[0];
        public GameObject[] columnPrefab = new GameObject[0];
        public GameObject defaultPrefab;
        public int[] rowHeight = new int[0];
        public int[] columnWidth = new int[0];
        public Transform[] rowBgPrefab = new Transform[0];
        public Transform[] rowBg = new Transform[0];
        // rows for background
        public UIWidget background;
        public Vector4 backgroundPadding;
        public bool resizeCollider;
        public bool expandColliderToPadding;
        public bool reuseCell;
        // Don't destroy cells when SetModel() called
        public bool propagateReposition = true;
        //  if true, Reposition is propagated to the ancestor UILayouts
        public GameObject emptyObj;
		
        private List<UIGridEventListener> listeners = new List<UIGridEventListener>();
        private UIGridModel model;
        private Vector2[,] cellPos;
        private bool ALWAYS_HORIZONTAL_BG = true;
        private Action<UIGridCell> initFunc = null;

        void Awake()
        {
            MakePrefabInactive(rowPrefab);
            MakePrefabInactive(columnPrefab);
            MakePrefabInactive(defaultPrefab);
        }

        /**
		 * @param t
		 * @return int get the serialized array index
		 */
        public int GetIndex(Transform t)
        {
            if (t != null)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == t)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Convert serialized array index to 2D array row index
        /// </summary>
        /// <returns>The row index.</returns>
        /// <param name="index">Index.</param>
        public int GetRowIndex(int index)
        {
            if (IsHorizontal())
            {
                return index / GetMaxPerLine();
            } else
            {
                return index % GetMaxPerLine();
            }
        }

        /// <summary>
        /// Convert serialized array index to 2D array column index
        /// </summary>
        /// <returns>The column index.</returns>
        /// <param name="index">Index.</param>
        public int GetColumnIndex(int index)
        {
            if (IsHorizontal())
            {
                return index % GetMaxPerLine();
            } else
            {
                return index / GetMaxPerLine();
            }
        }

        public int GetRowIndex(Transform t)
        {
            int i = GetIndex(t);
            if (i < 0)
            {
                return -1;
            }
            return GetRowIndex(i);
        }

        public int GetColumnIndex(Transform t)
        {
            int i = GetIndex(t);
            if (i < 0)
            {
                return -1;
            }
            return GetColumnIndex(i);
        }

        /**
		 * if Horizontal Arrangement, return column index
		 * if Vertical Arrangement, return row index
		 */
        public int GetLineIndex(int index)
        {
            return index % GetMaxPerLine();
        }

        /// <summary>
        /// Gets the row size.
        /// [NOTE] This is not the same as contents size.
        /// </summary>
        /// <returns>The row count.</returns>
        public int GetRowCount()
        {
            if (components.Length == 0)
            {
                return 0;
            }
            int row = IsHorizontal()? GetLineCount() : GetMaxPerLine();
            return Math.Max(row, rowHeader);
        }

        /**
		 * column count except row header
		 */
        public int GetContentsRowCount()
        {
            return GetRowCount()-rowHeader;
        }

        /// <summary>
        /// Gets the column size.
        /// [NOTE] This is not the same as contents size.
        /// </summary>
        /// <returns>The column count.</returns>
        public int GetColumnCount()
        {
            if (components.Length == 0)
            {
                return 0;
            }
            int col = IsHorizontal()? GetMaxPerLine() : GetLineCount();
            return Math.Max(col, columnHeader);
        }

        /**
		 * column count except column header
		 */
        public int GetContentsColumnCount()
        {
            return GetColumnCount()-columnHeader;
        }

        public int GetMaxPerLine()
        {
            return Mathf.Max(1, maxPerLine);
        }

        public int GetLineCount()
        {
            int l = components.Length / GetMaxPerLine();
            if (components.Length % GetMaxPerLine() != 0)
            {
                l++;
            }
            return l;
        }

        public int GetBackgroundRowCount()
        {
            if (ALWAYS_HORIZONTAL_BG||IsHorizontal())
            {
                return Math.Max(GetRowCount(), rowHeader);
            } else
            {
                return Math.Max(GetColumnCount(), columnHeader);
            }
        }

        public void SetContents(IList data, Action<UIGridCell> initFunc = null)
        {
            if (this.model == null)
            {
                SetModel(new UIGridModel(data, IsHorizontal(), maxPerLine), initFunc);
            } else
            {
                this.initFunc = initFunc;
                this.model.SetContents(data, IsHorizontal(), maxPerLine);
                RefreshContents();
            }
        }

        public void SetContents(IEnumerable data, Action<UIGridCell> initFunc = null)
        {
            if (this.model == null)
            {
                SetModel(new UIGridModel(data, IsHorizontal(), maxPerLine), initFunc);
            } else
            {
                this.model.SetContents(data, IsHorizontal(), maxPerLine);
                this.initFunc = initFunc;
                RefreshContents();
            }
        }

        public void SetModel(UIGridModel model, Action<UIGridCell> initFunc = null)
        {
            this.model = model;
            this.initFunc = initFunc;
            RefreshContents();
        }

        /// <summary>
        /// Used when the cell prefab is DummyGridCell
        /// </summary>
        /// <param name="count">Count.</param>
        public void SetDummyModel(int count)
        {
            int[] content = new int[count];
            SetModel(new UIGridModel(content, IsHorizontal(), maxPerLine));
        }

        public void Clear()
        {
            SetDummyModel(0);
        }

        public UIGridModel GetModel()
        {
            return this.model;
        }

        public bool IsHorizontal()
        {
            return arrangement == Arrangement.Horizontal;
        }

        public bool IsEmpty()
        {
            return components == null||components.Length == 0||components[0] == null;
        }

        public bool IsVertical()
        {
            return arrangement == Arrangement.Vertical;
        }

        [Obsolete("Change model instead")]
        public void AddRow(params Transform[] row)
        {
            AddRow(GetRowCount(), row);
        }

        [Obsolete("Change model instead")]
        public void AddRow(int rowIndex, params Transform[] row)
        {
            int colSize = GetColumnCount();
            int insertSize = row.Length;
            // modify insertion array size as the multiple of column size
            if (insertSize % colSize != 0)
            {
                insertSize = (insertSize / colSize+1) * colSize;
                Array.Resize(ref row, insertSize);
            }
            int index = GetIndex(rowIndex, 0);
            if (IsHorizontal())
            {
                Insert(index, row);
            } else
            {
                int unit = insertSize / colSize;
                for (int i = 0; i < colSize; i++)
                {
                    Transform[] ins = new Transform[unit];
                    Array.Copy(row, i * unit, ins, 0, unit);
                    Insert(index, ins);
                    index += unit+GetMaxPerLine();
                }
                rowHeight = rowHeight.Insert(rowIndex, 0);
                rowPrefab = rowPrefab.Insert(rowIndex, new GameObject[1] { null });
                valigns = valigns.Insert(rowIndex, valign);
                maxPerLine++;
            }
        }

        [Obsolete("Change model instead")]
        public void AddColumn(params Transform[] row)
        {
            AddColumn(GetColumnCount(), row);
        }

        [Obsolete("Change model instead")]
        public void AddColumn(int colIndex, params Transform[] col)
        {
            int rowSize = GetRowCount();
            int insertSize = col.Length;
            // modify insertion array size as the multiple of column size
            if (insertSize % rowSize != 0)
            {
                insertSize = (insertSize / rowSize+1) * rowSize;
                Array.Resize(ref col, insertSize);
            }
            if (IsVertical())
            {
                int index = GetIndex(0, colIndex);
                Insert(index, col);
            } else
            {
                int unit = insertSize / rowSize;
                int index = colIndex;
                for (int i = 0; i < rowSize; i++)
                {
                    Transform[] ins = new Transform[unit];
                    Array.Copy(col, i * unit, ins, 0, unit);
                    Insert(index, ins);
                    index += unit+GetMaxPerLine();
                }
                columnWidth = columnWidth.Insert(colIndex, 0);
                columnPrefab = columnPrefab.Insert(colIndex, new GameObject[1] { null });
                haligns = haligns.Insert(colIndex, halign);
                maxPerLine++;
            }
        }

        [Obsolete("Change model instead")]
        public void RemoveRow(int rowIndex)
        {
            if (IsHorizontal())
            {
                int index = GetIndex(rowIndex, 0);
                int count = GetMaxPerLine();
                // when last row is deleted, only remaining parts are removed.
                if (rowIndex == GetRowCount()-1&&components.Length % GetMaxPerLine() != 0)
                {
                    count = components.Length % GetMaxPerLine();
                }
                for (int i = 0; i < count; i++)
                {
                    DestroyCell(components[index+i]);
                }
                if (!reuseCell||!Application.isPlaying)
                {
                    components = components.Remove(index, count);
                }
            } else
            {
                int colSize = GetColumnCount();
                Transform[] newComponents = new Transform[GetLineCount() * (maxPerLine-1)];
                Array.Copy(components, 0, newComponents, 0, rowIndex);
                for (int c = 0; c < colSize; c++)
                {
                    int srcIndex = GetIndex(rowIndex, c);
                    long copySize = c < colSize-1? maxPerLine-1 : Math.Min(maxPerLine-1, components.Length-srcIndex-1);
                    Array.Copy(components, srcIndex+1, newComponents, c * (maxPerLine-1)+rowIndex, copySize);
                    if (components[srcIndex] != null)
                    {
                        if (reuseCell||(Application.isEditor&&!Application.isPlaying))
                        {
                            components[srcIndex].gameObject.SetActive(false);
                        } else
                        {
                            components[srcIndex].gameObject.DestroyEx();
                        }
                    }
                }
                if (!reuseCell||!Application.isPlaying)
                {
                    components = newComponents;
                    rowHeight = rowHeight.Remove(rowIndex);
                    rowPrefab = rowPrefab.Remove(rowIndex);
                    valigns = valigns.Remove(rowIndex);			
                    maxPerLine--;
                }
            }
        }

        [Obsolete("Change model instead")]
        public void RemoveColumn(int colIndex)
        {
            if (IsVertical())
            {
                int index = GetIndex(0, colIndex);
                int count = GetMaxPerLine();
                if (colIndex == GetColumnCount()-1&&components.Length % GetMaxPerLine() != 0)
                {
                    count = components.Length % GetMaxPerLine();
                }
                for (int i = 0; i < count; i++)
                {
                    DestroyCell(components[index+i]);
                }
                if (!reuseCell||!Application.isPlaying)
                {
                    components = components.Remove(index, count);
                }
            } else
            {
                int rowSize = GetRowCount();
                Transform[] newComponents = new Transform[GetLineCount() * (maxPerLine-1)];
                Array.Copy(components, 0, newComponents, 0, colIndex);
                for (int r = 0; r < rowSize; r++)
                {
                    int srcIndex = GetIndex(r, colIndex);
                    long copySize = r < rowSize-1? maxPerLine-1 : Math.Min(maxPerLine-1, components.Length-srcIndex-1);
                    Array.Copy(components, srcIndex+1, newComponents, r * (maxPerLine-1)+colIndex, copySize);
                    DestroyCell(components[srcIndex]);
                }
                if (!reuseCell)
                {
                    components = newComponents;
                    columnWidth = columnWidth.Remove(colIndex);
                    columnPrefab = columnPrefab.Remove(colIndex);
                    haligns = haligns.Remove(colIndex);
                    maxPerLine--;
                }
            }
        }

        private void DestroyCell(Transform c)
        {
            if (c == null)
            {
                return;
            }
            if (Application.isEditor&&!Application.isPlaying)
            {
                c.gameObject.SetActive(false);
            } else if (reuseCell)
            {
                c.GetComponent<UIGridCell>().Clear();
                c.gameObject.SetActive(false);
            } else
            {
                c.gameObject.DestroyEx();
            }
        }

        public Vector2 GetCellPos(int r, int c)
        {
            return cellPos[r, c];
        }

        private bool Resize<T>(ref T[] arr, int size)
        {
            T[] src = arr;
            Array.Resize(ref arr, size);
            return arr != src;
        }

        [Obsolete("Change model instead")]
        public void Add(params Transform[] t)
        {
            Insert(components.Length, t);
        }

        [Obsolete("Change model instead")]
        public void Insert(int i, params Transform[] t)
        {
            if (components.Length < i)
            {
                Array.Resize(ref components, i);
            }
            components = components.Insert(i, t);
            InitArray();
            InvalidateLayout();
        }

        [Obsolete("Change model instead")]
        public void Remove(Transform t)
        {
            int i = GetIndex(t);
            if (i >= 0)
            {
                components = components.Remove(i);
            }
            InitArray();
            InvalidateLayout();
        }

        public Transform GetBackground(int i)
        {
            int prefabIndex = i;
            if (ALWAYS_HORIZONTAL_BG||IsHorizontal())
            {
                prefabIndex -= rowHeader;
            } else
            {
                prefabIndex -= columnHeader;
            }
            if (prefabIndex < 0)
            {
                return rowBg[i];
            }
            if (rowBgPrefab.IsEmpty())
            {
                return null;
            }
            Transform prefab = rowBgPrefab[prefabIndex % rowBgPrefab.Length];
            if (rowBg[i] == null&&prefab != null)
            {
                rowBg[i] = NGUIUtil.InstantiateWidget(transform, prefab.gameObject);
                rowBg[i].gameObject.SetActive(true);
                rowBg[i].name = "row_bg".AddSuffix(i);
            }
            return rowBg[i];
        }

        public bool InitArray()
        {
            int rowCount = IsVertical()? GetMaxPerLine() : GetLineCount();
            int colCount = IsHorizontal()? GetMaxPerLine() : GetLineCount();
            bool changed = Resize(ref rowHeight, rowCount);
            changed |= Resize(ref columnWidth, colCount);
            if (!Application.isPlaying)
            {
                changed |= Resize(ref rowPrefab, rowCount);
                changed |= Resize(ref columnPrefab, colCount);
            }
            int bgCount = GetBackgroundRowCount();
            if (haligns.Length != colCount)
            {
                int c = haligns.Length;
                changed |= Resize(ref haligns, colCount);
                for (; c < colCount; c++)
                {
                    haligns[c] = halign;
                    changed = true;
                }
            }
            if (valigns.Length != rowCount)
            {
                int r = valigns.Length;
                changed |= Resize(ref valigns, rowCount);
                for (; r < rowCount; r++)
                {
                    valigns[r] = valign;
                    changed = true;
                }
            }
            // Remove Unused Background
            for (int i = bgCount; i < rowBg.Length; i++)
            {
                if (rowBg[i] != null)
                {
                    rowBg[i].gameObject.DestroyEx();
                }
            }
            changed |= Resize(ref rowBg, bgCount);
			
            if (cellPos == null||cellPos.GetLength(0) != rowCount||cellPos.GetLength(1) != colCount)
            {
                cellPos = new Vector2[rowCount, colCount];
            }
            if (changed)
            {
                InvalidateLayout();
            }
            return changed;
        }

        private int GetIndex(int row, int col)
        {
            return GetIndex(arrangement, maxPerLine, row, col);
        }

        private static int GetIndex(Arrangement arrangement, int maxPerLine, int row, int col)
        {
            int i = 0;
            if (arrangement == Arrangement.Horizontal)
            {
                i = row * maxPerLine+col;
            } else
            {
                i = row+col * maxPerLine;
            }
            return i;
        }

        public Transform GetCell(int row, int col)
        {
            int i = GetIndex(row, col);
            if (i >= components.Length)
            {
                return null;
            }
            return components[i];
        }

        public void SetCell(int row, int col, Transform t)
        {
            int i = GetIndex(row, col);
            if (i >= components.Length)
            {
                Resize(ref components, i+1);
            }
            components[i] = t;
            InvalidateLayout();
        }

        public void AddListener(UIGridEventListener l)
        {
#if UNITY_EDITOR
            Assert.IsFalse(listeners.Contains(l));
#endif
            this.listeners.Add(l);
        }

        public void RemoveListener(UIGridEventListener l)
        {
            this.listeners.Remove(l);
        }

        /// <summary>
        /// Recalculate the position of all elements within the grid, sorting them alphabetically if necessary.
        /// </summary>
        override protected void DoLayout()
        {
//			if (this.model == null) {
//				return new Bounds();
//			}
            InitArray();
            Rect bound = new Rect();
			
            int row = GetRowCount();
            int col = GetColumnCount();
			
            bounds = new Bounds[row, col];
            Transform[,] transforms = new Transform[row, col];

            for (int i = 0, imax = components.Length; i < imax; ++i)
            {
                int r = GetRowIndex(i);
                int c = GetColumnIndex(i);
                //			int line = GetLineIndex(i);
                transforms[r, c] = components[i];
                if (cellSize.x != 0&&cellSize.y != 0)
                {
                    float cx = r * cellSize.x+cellSize.x * 0.5f+padding.x * Mathf.Max(0, r-1);
                    float cy = c * cellSize.y+cellSize.y * 0.5f+padding.y * Mathf.Max(0, c-1);
                    ;
                    bounds[r, c] = new Bounds(new Vector3(cx, cy), new Vector3(cellSize.x, cellSize.y, 1));
                } else
                {
                    bounds[r, c] = CalculateBounds(components[i]);
                }
            }
			
            // max height
            float[] maxHeights = new float[row];
            for (int r = 0; r < row; r++)
            {
                // check if null row
                bool filled = false;
                for (int c = 0; c < col&&!filled; c++)
                {
                    var cell = GetCell(r, c);
                    if (cell != null&&cell.gameObject.activeSelf)
                    {
                        filled = true; 
                    }
                }
                if (filled)
                {
                    if (r < rowHeight.Length&&rowHeight[r] != 0)
                    {
                        maxHeights[r] = rowHeight[r];
                    } else if (cellSize.y != 0)
                    {
                        maxHeights[r] = cellSize.y;
                    } else
                    {
                        for (int c = 0; c < col; c++)
                        {
                            maxHeights[r] = Mathf.Max(maxHeights[r], bounds[r, c].size.y, cellMinSize.y);
                        }
                    }
                    bound.height += maxHeights[r];
                }
            }
            bound.height += (row-1) * padding.y;
			
            float[] maxWidths = new float[col];
            for (int c = 0; c < col; c++)
            {
                // check if null row
                bool filled = false;
                for (int r = 0; r < row&&!filled; r++)
                {
                    var cell = GetCell(r, c);
                    if (cell != null&&cell.gameObject.activeSelf)
                    {
                        filled = true; 
                    }
                }
                if (filled)
                {
                    if (cellSize.x != 0)
                    {
                        maxWidths[c] = cellSize.x;
                    } else if (c < columnWidth.Length&&columnWidth[c] != 0)
                    {
                        maxWidths[c] = columnWidth[c];
                    } else
                    {
                        for (int r = 0; r < row; r++)
                        {
                            maxWidths[c] = Mathf.Max(maxWidths[c], bounds[r, c].size.x, cellMinSize.x);
                        }
                    }
                    bound.width += maxWidths[c];
                }
            }
			
            float cellTotalWidth = bound.width;
            bound.width += (col-1) * padding.x;
			
            // expand cell width by ratio
            if (totalWidth > 0&&bound.width < totalWidth)
            {
                float pad = totalWidth-bound.width;
                for (int c = 0; c < col; c++)
                {
                    maxWidths[c] += pad * (maxWidths[c] / cellTotalWidth);
                }
                bound.width = totalWidth;
            }

            UIGridPrefabs prefabs = GetPrefabs();
            float pixely = 0;
            for (int r = 0; r < row; r++)
            {
                float pixelx = 0;
                bool activeRow = false; // inactive row is removed from layout computation
                for (int c = 0; c < col; c++)
                {
                    Transform t = transforms[r, c];
                    if (t != null&&t.gameObject.activeInHierarchy)
                    {
                        if (!t.IsChildOf(transform))
                        {
                            t.SetParent(transform, false);
                        }
                        Vector3 point = bounds[r, c].min;
                        Vector3 size = bounds[r, c].size;
                        float halignPad = 0;
                        float valignPad = 0;
                        Vector3 pos = t.localPosition;
                        if (haligns[c] == HAlign.None)
                        {
                            if (r >= rowHeader&&c >= columnHeader)
                            {
                                GameObject prefab = prefabs.GetPrefab(r, c);
                                if (prefab != null)
                                {
                                    if (prefab == defaultPrefab)
                                    {
                                        pos.x = pixelx+prefab.transform.localPosition.x;
                                    } else
                                    {
                                        pos.x = prefab.transform.localPosition.x;
                                    }
                                }
                            }
                        } else
                        {
                            if (haligns[c] == HAlign.Center)
                            {
                                halignPad = (maxWidths[c]-size.x) / 2f;
                            } else if (haligns[c] == HAlign.Right)
                            {
                                halignPad = maxWidths[c]-size.x;
                            }
                            pos.x = pixelx-point.x+halignPad;
                        }
                        if (valigns[r] == VAlign.None)
                        {
                            if (r >= rowHeader&&c >= columnHeader)
                            {
                                GameObject prefab = prefabs.GetPrefab(r, c);
                                if (prefab != null)
                                {
                                    if (prefab == defaultPrefab)
                                    {
                                        pos.y = pixely+prefab.transform.localPosition.y;
                                    } else
                                    {
                                        pos.y = prefab.transform.localPosition.y;
                                    }
                                }
                            }
                        } else
                        {
                            if (valigns[r] == VAlign.Center)
                            {
                                valignPad = (maxHeights[r]-size.y) / 2f;
                            } else if (valigns[r] == VAlign.Bottom)
                            {
                                valignPad = maxHeights[r]-size.y;
                            }
                            pos.y = pixely-(point.y+size.y)-valignPad;
                        }
						
                        t.SetLocalPosition(pos, 0.01f);
                        NGUIUtil.ApplyPivot(t);
						
                        // update Collider Bound
                        if (resizeCollider)
                        {
                            BoxCollider box = t.GetComponentInChildren<BoxCollider>();
                            if (box != null)
                            {
                                Vector3 center = box.center; 
                                center.x = pixelx+maxWidths[c] * 0.5f;
                                center.y = pixely-maxHeights[r] * 0.5f;
                                Vector3 boxSize = box.size;
                                boxSize.x = maxWidths[c];
                                boxSize.y = maxHeights[r];
                                if (expandColliderToPadding)
                                {
                                    if (c < col-1)
                                    {
                                        boxSize.x += padding.x;
                                        center.x += padding.x * 0.5f;
                                    }
                                    if (r < row-1)
                                    {
                                        boxSize.y += padding.y;
                                        center.y -= padding.y * 0.5f;
                                    }
                                }
                                center = transform.TransformSpace(center, t);
                                center.z = box.center.z;
                                box.center = center;
                                box.size = boxSize;
                            } else
                            {
                                BoxCollider2D box2d = t.GetComponentInChildren<BoxCollider2D>();
                                if (box2d != null)
                                {
                                    Vector2 center = box2d.offset; 
                                    center.x = pixelx+maxWidths[c] * 0.5f;
                                    center.y = pixely-maxHeights[r] * 0.5f;
                                    Vector2 boxSize = box2d.size;
                                    boxSize.x = maxWidths[c];
                                    boxSize.y = maxHeights[r];
                                    if (expandColliderToPadding)
                                    {
                                        if (c < col-1)
                                        {
                                            boxSize.x += padding.x;
                                            center.x += padding.x * 0.5f;
                                        }
                                        if (r < row-1)
                                        {
                                            boxSize.y += padding.y;
                                            center.y -= padding.y * 0.5f;
                                        }
                                    }
                                    center = transform.TransformSpace(center, t);
                                    box2d.offset = center;
                                    box2d.size = boxSize;
                                }
                            }
                        }
                        //					if (bound.width != 0) {
                        //						bound.x = Math.Min(bound.x, pos.x);
                        //					}
                        //					if (bound.height != 0) {
                        //						bound.y = Math.Max(bound.y, pos.y);
                        //					}
                    }
                    Vector3 extent = bounds[r, c].extents;
                    bounds[r, c].center = new Vector2(pixelx+extent.x, pixely-extent.y);

                    cellPos[r, c] = new Vector2(pixelx, pixely);
                    activeRow |= t == null||t.gameObject.activeInHierarchy;
                    pixelx += maxWidths[c]+padding.x;
                }
                if (activeRow)
                {
                    pixely += -maxHeights[r]-padding.y;
                }
            }
			
            if (NGUIUtil.IsValid(bound))
            {
                DoLayoutBackground(ref bound, maxWidths, maxHeights);
//				UIDraggablePanel drag = NGUITools.FindInParents<UIDraggablePanel>(gameObject);
//				if (drag != null) drag.UpdateScrollbars(true);
            } else
            {
                bound = new Rect();
            }
            if (propagateReposition)
            {
                NGUIUtil.Reposition(transform);
            }
        }

        private void DoLayoutBackground(ref Rect bound, float[] widths, float[] heights)
        {
            float bgWidth = bound.width;
            float bgHeight = bound.height;
            float x = bound.x;
            float y = bound.y;
			
            // exclude header size from row background size 
            if (IsHorizontal())
            {
                for (int i = 0; i < columnHeader; i++)
                {
                    float w = widths[i]+padding.x;
                    x += w;
                    bgWidth -= w;
                }
            } else
            {
                for (int i = 0; i < rowHeader; i++)
                {
                    float h = heights[i]-padding.y;
                    y += h;
                    bgHeight -= h;
                }
            }
			
            for (int i = 0, imax = GetBackgroundRowCount(); i < imax; i++)
            {
                Transform t = GetBackground(i);
                if (t != null&&t.gameObject.activeInHierarchy)
                {
                    Transform tbg = t;
                    UISprite sprite = t.GetComponentInChildren<UISprite>();
                    if (sprite != null)
                    {
                        tbg = sprite.transform;
                    }
                    Vector3 pos = tbg.localPosition;
                    pos.x = x;
                    pos.y = y;
                    // set size
                    if (ALWAYS_HORIZONTAL_BG||IsHorizontal())
                    {
                        tbg.localScale = new Vector3(bgWidth, -heights[i], 1);
                    } else
                    {
                        tbg.localScale = new Vector3(widths[i], -bgHeight, 1);
                    }
                    Bounds bound0 = CalculateBounds(t);
                    Vector3 point = bound0.min;
                    // align position
                    pos.x -= point.x;
                    pos.y -= point.y;
                    t.localPosition = pos;
					
                    // Update Bounds
                    BoxCollider box = t.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        NGUIUtil.UpdateCollider(box);
                        // Set Listener
                        UIButton msg = t.GetComponent<UIButton>();
                        if (msg != null)
                        {
                            EventDelegate method = new EventDelegate(this, "OnRowSelected");
                            method.parameters[0].obj = this;
                            method.parameters[0].field = "";
                            // i-rowHeader
                            msg.onClick.Add(method);
                        }
                    }
					
                }
                if (ALWAYS_HORIZONTAL_BG||IsHorizontal())
                {
                    y += heights[i]-padding.y;
                } else
                {
                    x += widths[i]+padding.x;
                }
            }
			
            if (background != null)
            {
                bound.x -= backgroundPadding.x;
                bound.y -= backgroundPadding.y;
                bound.width += backgroundPadding.x+backgroundPadding.z;
                bound.height += backgroundPadding.y+backgroundPadding.w;
                Rect bgBound = background.transform.parent.TransformRect(transform, bound);
                NGUIUtil.SetBoundingBox(background.transform, bgBound);
            }
        }

        private Bounds CalculateBounds(Transform t)
        {
            if (t == null||!t.gameObject.activeInHierarchy)
            {
                return new Bounds();
            }
            UIGridCell cell = t.GetComponent<UIGridCell>();
            if (cell != null&&cell.bound != null)
            {
                return cell.bound.CalculateBounds(cell.transform);
            }
            return GetBounds(t);
        }

        private UIGridPrefabs GetPrefabs()
        {
            return new UIGridPrefabs(defaultPrefab, rowPrefab, columnPrefab, rowHeader, columnHeader);
        }

        public void RefreshContents()
        {
            if (model == null)
            {
                emptyObj.SetActiveEx(true);
                return;
            }
            List<object> sel = GetSelectedDataList<object>();
            emptyObj.SetActiveEx(model.IsEmpty());
            AssertDimension();
            UIGridPrefabs prefabs = GetPrefabs();
            if (IsHorizontal())
            {
                for (int r = 0; r < model.GetRowCount(); r++)
                {
                    for (int c = 0; c < model.GetColumnCount(); c++)
                    {
                        object cellValue = model.GetValue(r, c);
                        SetCellValue(prefabs, r+rowHeader, c+columnHeader, cellValue, initFunc);
                    }
                }
                /*  여분의 Row를 삭제한다. */
                for (int r = GetRowCount()-1; r >= rowHeader+model.GetRowCount(); r--)
                {
                    #pragma warning disable 0618
                    RemoveRow(r);
                    #pragma warning restore 0618
                }
            } else
            {
                for (int c = 0; c < model.GetColumnCount(); c++)
                {
                    for (int r = 0; r < model.GetRowCount(); r++)
                    {
                        object cellValue = model.GetValue(r, c);
                        SetCellValue(prefabs, r+rowHeader, c+columnHeader, cellValue, initFunc);
                    }
                }
                for (int c = GetColumnCount()-1; c >= columnHeader+model.GetColumnCount(); c--)
                {
                    #pragma warning disable 0618
                    RemoveColumn(c);
                    #pragma warning restore 0618
                }
            }
            if (enabled)
            {
                DoLayout();
            }
            SelectCell<object>(o => sel.Contains(o));
			
            foreach (UIGridEventListener l in listeners)
            {
                l.OnModelChanged();
            }
        }

        /**
		 * Cell이 비었을 경우 prefab으로 새로 생성하여 Cell에 집어넣는다.
		 * cell 값이 GameObject일 경우 prefab이라고 가정하고 새로 생성한다.
		 * 이외의 값일 경우 ToString()을 사용하여 Label을 넣는다.
		 */
        private void SetCellValue(UIGridPrefabs prefabs, int row, int col, object cellValue, Action<UIGridCell> initFunc)
        {
            Transform cell = GetCell(row, col);
            if (cellValue == null)
            {
                if (cell != null)
                {
                    cell.gameObject.SetActive(false);
                }
                return;
            }
			
            // Instantiate Cell
            if (cell == null)
            {
                cell = prefabs.Instantiate(row, col);
                if (!cell.IsChildOf(transform))
                {
                    cell.SetParent(transform, false);
                }
            }
            // Set Cell Value
            SetCell(row, col, cell);
			
            UIGridCell.SetValue(this, cell, row-rowHeader, col-columnHeader, cellValue, initFunc);
        }

        private void AssertDimension()
        {
            Assert.IsTrue(model.IsEmpty()
            ||(IsHorizontal()&&GetMaxPerLine()-columnHeader == model.GetColumnCount())
            ||(IsVertical()&&GetMaxPerLine()-rowHeader == model.GetRowCount()),
                "[Grid]{0} [Model]{1} [Grid]{2}x{3} [Model]{4}x{5} [Header]{6}x{7} ",
                name, model.GetType().FullName, 
                GetRowCount(), GetColumnCount(),
                model.GetRowCount(), model.GetColumnCount(),
                rowHeader, columnHeader
            );
        }

        [NoObfuscate]
        public void OnRowSelected(object row)
        {
            int rowNo = (int)row;
            foreach (UIGridEventListener l in listeners)
            {
                l.OnRowSelected(rowNo);
            }
        }

        override protected void UpdateImpl()
        {
            if (model != null)
            {
                model.Update(RefreshContents);
            }
        }

        /**
		 * Row중 UILabel인 경우 text color를 바꾸어준다.
		 * @param row row 값은 title row를 포함한 실제 row번호(zero-based)
		 */
        public void SetRowColor(int row, Color color)
        {
            string colorStr = NGUIUtil.ConvertColor2Str(color);
            for (int col = columnHeader; col < GetColumnCount(); col++)
            {
                Transform t = GetCell(row+rowHeader, col);
                if (t != null)
                {
                    UILabel label = t.GetComponent<UILabel>();
                    if (label != null)
                    {
                        label.SetText(colorStr+label.text);
                    }
                }
            }
        }

        private void MakePrefabInactive(params GameObject[] prefab)
        {
            if (prefab != null&&Application.isPlaying)
            {
                foreach (GameObject r in prefab)
                {
                    if (r != null)
                    {
                        r.SetActive(false);
                        int index = GetIndex(r.transform);
                        if (index >= 0)
                        {
                            components[index] = null;
                        }
                    }
                }
            }
        }

        public void ForEach<T>(Predicate<T> func, bool includeInactive = false) where T: Component
        {
            foreach (Transform t in components)
            {
                if (t != null&&(includeInactive||t.gameObject.activeSelf))
                {
                    T cell = t.GetComponent<T>();
                    if (cell != null&&!func(cell))
                    {
                        return;
                    }
                }
            }
        }

        public void ForEach<T>(Action<T> func, bool includeInactive = false) where T: Component
        {
            foreach (Transform t in components)
            {
                if (t != null&&(includeInactive||t.gameObject.activeSelf))
                {
                    T cell = t.GetComponent<T>();
                    if (cell != null)
                    {
                        func(cell);
                    }
                }
            }
        }

        public int GetCount<T>(Predicate<T> predicate) where T: Component
        { 
            int count = 0;
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    T cell = t.GetComponent<T>();
                    if (cell != null&&predicate(cell))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        public List<V> ConvertCells<C, V>(Converter<C, V> conv) where C: UIGridCell
        { 
            List<V> list = new List<V>();
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    C cell = t.GetComponent<C>();
                    if (cell != null)
                    {
                        V v = conv(cell);
                        if (v != null)
                        {
                            list.Add(v);
                        }
                    }
                }
            }
            return list;
        }

        public List<C> FilterCell<C>(Predicate<C> func) where C:UIGridCell
        { 
            List<C> list = new List<C>();
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    C cell = t.GetComponent<C>();
                    if (cell != null&&func(cell))
                    {
                        list.Add(cell);
                    }
                }
            }
            return list;
        }

        public List<D> FilterCellData<D>(Predicate<D> func)
        { 
            List<D> list = new List<D>();
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    UIGridCell cell = t.GetComponent<UIGridCell>();
                    if (cell != null)
                    {
                        D cellData = cell.GetCellData<D>();
                        if (cellData != null&&func(cellData))
                        {
                            list.Add(cellData);
                        }
                    }
                }
            }
            return list;
        }

        public C GetSelectedCell<C>() where C: UIGridCell
        { 
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    C cell = t.GetComponent<C>();
                    if (cell != null&&cell.gameObject.activeSelf&&cell.toggle != null&&cell.toggle.value)
                    {
                        return cell;
                    }
                }
            }
            return null;
        }

        public List<C> GetSelectedCellList<C>() where C: UIGridCell
        { 
            return ConvertCells<C, C>(c =>
            {
                if (c.toggle != null&&c.toggle.value)
                {
                    return c;
                } else
                {
                    return null;
                }
            });
        }

        public C GetSelectedData<C>()
        { 
            for (int r = rowHeader; r < GetRowCount(); ++r)
            {
                for (int c = columnHeader; c < GetColumnCount(); ++c)
                {
                    UIGridCell cell = GetCell(r, c).GetComponent<UIGridCell>();
                    if (cell.toggle != null&&cell.toggle.value)
                    {
                        return (C)cell.GetCellData();
                    }
                }
            }
            return default(C);
        }

        public List<C> GetSelectedDataList<C>()
        { 
            return ConvertCells<UIGridCell, C>(c =>
            {
                if (c.toggle != null&&c.toggle.value)
                {
                    return (C)c.GetCellData();
                } else
                {
                    return default(C);
                }
            });
        }

        public bool SelectCell<D>(Predicate<D> predicate, bool includeInactive = false)
        { 
            bool selected = false;
            ForEach<UIGridCell>(c =>
            {
                if (c.toggle != null)
                {
                    // Setting grouped toggle on inactive toggle incurs an abnormal behaviour.
                    if (c.toggle.isActiveAndEnabled||c.toggle.group == 0)
                    {
                        bool s = predicate(c.GetCellData<D>());
                        c.SetSelected(s);
                    }
                }
                return true;
            }, includeInactive);
            return selected;
        }

        public void SelectCell(object cellData)
        { 
            ForEach<UIGridCell>(cell =>
            {
                if (cell.toggle != null)
                {
                    // Setting grouped toggle on inactive toggle incurs an abnormal behaviour.
                    if (cell.toggle.isActiveAndEnabled||cell.toggle.group == 0)
                    {
                        bool select = cell.GetCellData() == cellData;
                        cell.SetSelected(select);
                    }
                }
            });
        }

        public void SelectCell(int row, int col)
        { 
            UIGridCell cell = GetCell(row, col).GetComponent<UIGridCell>();
            if (cell != null&&cell.toggle != null&&cell.gameObject.activeSelf)
            {
                cell.SetSelected(true);
            }
        }

        /// <summary>
        /// Select one cell predicate is met. If none, select the first one.
        /// </summary>
        /// <returns>The one.</returns>
        /// <param name="predicate">Predicate.</param>
        /// <typeparam name="C">CellData type</typeparam>
        public D SelectOne<D>(Predicate<D> predicate)
        {
            D select = default(D);
            List<D> list = FilterCellData<D>(predicate);
            if (list.IsNotEmpty())
            {
                select = list[0];
            } else
            {
                UIGridCell c = GetCell(0, 0).GetComponent<UIGridCell>();
                if (c != null)
                {
                    select = c.GetCellData<D>();
                }
            }
            if (select != null)
            {
                SelectCell(select);
            }
            return select;
        }

        #region IEnumerable

        IEnumerator<Transform> IEnumerable<Transform>.GetEnumerator()
        {
            foreach (Transform t in components)
            {
                if (t != null&&t.gameObject.activeSelf)
                {
                    yield return t;
                }
            }
            //		return ((IEnumerable<Transform>)components).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return components.GetEnumerator();
        }

        #endregion

        public void ResetPosition()
        {
            UIScrollView view = GetComponentInParent<UIScrollView>();
            if (view != null)
            {
                view.ResetPosition();
            }
        }
		
        #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (bounds == null)
            {
                return;
            }
            Gizmos.matrix = transform.localToWorldMatrix;

            Bounds b = new Bounds();
            if (UnityEditor.Selection.activeGameObject == gameObject)
            {
                b = GetBounds();
            } else
            {
                // FIXMEM grid layout GIZMO
//				for (int r=0; r<bounds.GetLength(0) && b.size == Vector3.zero; r++) {
//					for (int c=0; c<bounds.GetLength(1) && b.size == Vector3.zero; c++) {
//						if (GetCell(r, c) != null && UnityEditor.Selection.activeGameObject == GetCell(r, c).gameObject) {
//							b = bounds[r,c];
//						}
//					}
//				}
            }
            DrawBounds(b);
        }

        private void DrawBounds(Bounds b)
        {
            float gizmoSize = 10;
            Vector3 size = b.size;
            if (size != Vector3.zero)
            {
                Gizmos.color = gizmoColor;
                Vector3 point = b.center;
                Gizmos.DrawWireCube(new Vector3(point.x, point.y, 1), new Vector3(size.x, size.y, 1));
                Gizmos.color = new Color(1-gizmoColor.r, 1-gizmoColor.g, 1-gizmoColor.b, 1);
                Gizmos.DrawCube(b.min, new Vector3(gizmoSize, gizmoSize, 1));
                Gizmos.DrawCube(b.max, new Vector3(gizmoSize, gizmoSize, 1));
                Gizmos.DrawCube(new Vector3(b.min.x, b.max.y, 1), new Vector3(gizmoSize, gizmoSize, 1));
                Gizmos.DrawCube(new Vector3(b.max.x, b.min.y, 1), new Vector3(gizmoSize, gizmoSize, 1));
                Gizmos.DrawWireSphere(b.center, 5);
            }
        }
        #endif
    }
}