﻿using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl
{
    public abstract class CommonDrawTargetBase : IFumenEditorDrawingTarget
    {
        protected IFumenEditorDrawingContext target;

        public abstract IEnumerable<string> DrawTargetID { get; }
        public abstract int DefaultRenderOrder { get; }
        private int? currentRenderOrder = default;
        public int CurrentRenderOrder
        {
            get => currentRenderOrder ?? DefaultRenderOrder; set
            {
                currentRenderOrder = value;
            }
        }

        public bool IsEnable { get; set; } = true;

        public virtual void Begin(IFumenEditorDrawingContext target)
        {
            target.PerfomenceMonitor.OnBeginTargetDrawing(this);
            this.target = target;
        }

        public abstract void Post(OngekiObjectBase ongekiObject);

        public virtual void End()
        {
            target.PerfomenceMonitor.OnAfterTargetDrawing(this);
            target = default;
        }
    }

    public abstract class CommonDrawTargetBase<T> : CommonDrawTargetBase where T : OngekiObjectBase
    {
        public abstract void Draw(IFumenEditorDrawingContext target, T obj);
        public override void Post(OngekiObjectBase ongekiObject) => Draw(target, (T)ongekiObject);
    }

    public abstract class CommonBatchDrawTargetBase<T> : CommonDrawTargetBase where T : OngekiObjectBase
    {
        private List<T> drawObjects = new();

        public abstract void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<T> objs);

        public override void End()
        {
            DrawBatch(target, drawObjects);
            drawObjects.Clear();

            base.End();
        }

        public override void Post(OngekiObjectBase ongekiObject)
        {
            drawObjects.Add((T)ongekiObject);
        }
    }
}
