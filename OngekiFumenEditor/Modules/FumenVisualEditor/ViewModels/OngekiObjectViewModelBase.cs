﻿using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public abstract class OngekiObjectViewModelBase : PropertyChangedBase, IViewAware
    {
        protected IOngekiObject referenceOngekiObject;

        public virtual IOngekiObject ReferenceOngekiObject
        {
            get { return referenceOngekiObject; }
            set
            {
                referenceOngekiObject = value;
                NotifyOfPropertyChange(() => ReferenceOngekiObject);
            }
        }

        private double x;

        public double X
        {
            get { return x; }
            set
            {
                x = value;
                NotifyOfPropertyChange(() => X);
            }
        }

        private double y;

        public event EventHandler<ViewAttachedEventArgs> ViewAttached;

        public double Y
        {
            get { return y; }
            set
            {
                y = value;
                NotifyOfPropertyChange(() => Y);
            }
        }

        public object View { get; private set; }

        public object Context { get; private set; }

        /// <summary>
        /// 表示此物件是否能设置水平位置(即此物件是否支持XGrid)
        /// </summary>
        public bool CanMoveX => ReferenceOngekiObject is IHorizonPositionObject;

        /// <summary>
        /// 表示此物件是否能设置时间轴位置(即此物件是否支持TGrid)
        /// </summary>
        public bool IsTimelineObject => ReferenceOngekiObject is ITimelineObject;

        public FumenVisualEditorViewModel EditorViewModel { get; set; }

        protected virtual void OnAttachedView(object view)
        {
            var element = view as FrameworkElement;
            element.SetBinding(Canvas.LeftProperty, "X");
            element.SetBinding(Canvas.TopProperty, "Y");

            Refresh();
        }

        public void AttachView(object view, object context = null)
        {
            View = view;
            Context = context;

            OnAttachedView(View);
        }

        public object GetView(object context = null) => View;
    }

    public abstract class DisplayObjectViewModelBase<T> : OngekiObjectViewModelBase where T : IOngekiObject, new()
    {
        public override IOngekiObject ReferenceOngekiObject
        {
            get
            {
                if (referenceOngekiObject is null)
                {
                    ReferenceOngekiObject = new T();
                }
                return base.ReferenceOngekiObject;
            }
            set
            {
                base.ReferenceOngekiObject = value;
            }
        }
    }
}