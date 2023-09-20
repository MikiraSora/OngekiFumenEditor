﻿using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OngekiFumenEditor.Utils;
using Mono.Cecil;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;
using Caliburn.Micro;
using Gemini.Modules.UndoRedo.Services;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using static OngekiFumenEditor.Base.OngekiObjects.Flick;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IFumenEditorClipboard))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultFumenEditorClipboard : IFumenEditorClipboard
    {
        private Dictionary<OngekiObjectBase, Point> currentCopiedSources = new();
        private FumenVisualEditorViewModel sourceEditor;

        public bool ContainPastableObjects => sourceEditor is not null && currentCopiedSources.Any();

        public async Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects)
        {
            if (sourceEditor.IsLocked)
                return;
            if (sourceEditor.Fumen is null)
                return;
            if (!sourceEditor.IsDesignMode)
            {
                sourceEditor.ToastNotify($"仅在设计模式下进行复制操作");
                return;
            }
            if (objects.IsEmpty())
            {
                sourceEditor.ToastNotify($"清空复制列表");
                return;
            }

            //清空一下
            currentCopiedSources.Clear();
            this.sourceEditor = default;

            foreach (var obj in objects.Where(x => x switch
            {
                //不允许被复制
                ConnectableObjectBase and not (ConnectableStartObject) => false,
                LaneCurvePathControlObject => false,
                LaneBlockArea.LaneBlockAreaEndIndicator => false,
                Soflan.SoflanEndIndicator => false,
                //允许被复制
                _ => true,
            }))
            {
                //这里还是得再次详细过滤:
                // * Hold头可以直接被复制
                // * 轨道如果是整个轨道节点都被选中，那么它也可以被复制，否则就不准
                if (obj is ConnectableStartObject start && obj is not Hold)
                {
                    //检查start轨道节点是否全被选中了
                    if (!start.Children.OfType<ConnectableObjectBase>().Append(start).All(x => x.IsSelected))
                        continue;
                }

                var x = 0d;
                if (obj is IHorizonPositionObject horizon)
                    x = XGridCalculator.ConvertXGridToX(horizon.XGrid, sourceEditor);

                var y = 0d;
                if (obj is ITimelineObject timeline)
                    y = TGridCalculator.ConvertTGridToY_DesignMode(timeline.TGrid, sourceEditor);

                var canvasPos = new Point(x, y);

                var source = obj as OngekiObjectBase;
                var copied = source?.CopyNew();
                if (copied is null)
                    continue;

                switch (copied)
                {
                    //特殊处理ConnectableStart:连Child和Control一起复制了,顺便删除RecordId(添加时需要重新分配而已)
                    case ConnectableStartObject _start:
                        _start.CopyEntireConnectableObject((ConnectableStartObject)source);
                        _start.RecordId = -1;
                        break;
                    //特殊处理LBK:连End物件一起复制了
                    case LaneBlockArea _lbk:
                        _lbk.CopyEntire((LaneBlockArea)source);
                        break;
                    //特殊处理SFL:连End物件一起复制了
                    case Soflan _sfl:
                        _sfl.CopyEntire((Soflan)source);
                        break;
                    //特殊处理Hold:清除Id
                    case Hold hold:
                        hold.ReferenceLaneStart = default;
                        break;
                    //特殊处理子弹类:克隆一份子弹模板
                    case IBulletPalleteReferencable bulletPalleteObject:
                        if (bulletPalleteObject.ReferenceBulletPallete is BulletPallete bpl)
                            bulletPalleteObject.ReferenceBulletPallete = bpl.CopyNew() as BulletPallete;
                        break;
                    default:
                        break;
                }

                //注册,并记录当前位置
                currentCopiedSources[copied] = canvasPos;
            }

            if (currentCopiedSources.Count == 0)
                sourceEditor.ToastNotify($"清空复制列表");
            else
            {
                this.sourceEditor = sourceEditor;
                sourceEditor.ToastNotify($"钦定 {currentCopiedSources.Count} 个物件作为复制源 {(currentCopiedSources.Count == 1 ? ",并作为刷子模式的批量生成源" : string.Empty)}");
            }
            return;
        }

        public async Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteMirrorOption mirrorOption, Point? placePoint = null)
        {
            if (targetEditor.IsLocked)
                return;
            if (sourceEditor is null)
            {
                Log.LogWarn($"无法粘贴因为sourceEditor为空");
                return;
            }
            if (currentCopiedSources.Count is 0)
            {
                Log.LogWarn($"无法粘贴因为复制列表为空");
                return;
            }

            bool isSameEditorCopy = sourceEditor == targetEditor;
            //convert y form sourceEditor to targetEditor
            double adjustY(double y)
            {
                if (isSameEditorCopy)
                    return y;
                var offsetTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(y, sourceEditor);
                var fixedY = TGridCalculator.ConvertTGridToY_DesignMode(offsetTGrid, targetEditor);
                return fixedY;
            }

            //计算出镜像中心位置
            var mirrorYOpt = CalculateYMirror(currentCopiedSources.Keys, mirrorOption);
            var mirrorXOpt = CalculateXMirror(targetEditor, currentCopiedSources.Keys, mirrorOption);

            var sourceCenterPos = CalculateRangeCenter(currentCopiedSources.Keys);
            var fixedY = adjustY(sourceCenterPos.Y);
            var fixedCenterPos = new Point(sourceCenterPos.X, fixedY);
            var offset = (placePoint ?? fixedCenterPos) - fixedCenterPos;

            if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                offset.X = 0;

            var redo = new System.Action(() => { });
            var undo = new System.Action(() => { });

            foreach (var pair in currentCopiedSources)
            {
                var source = pair.Key;
                var sourceCanvasPos = pair.Value;

                var copied = source.CopyNew();
                if (copied is null)
                    continue;

                switch (copied)
                {
                    //特殊处理ConnectableStart:连Child和Control一起复制了,顺便删除RecordId(添加时需要重新分配而已)
                    case ConnectableStartObject _start:
                        _start.CopyEntireConnectableObject((ConnectableStartObject)source);
                        redo += () => _start.RecordId = -1;
                        break;
                    //特殊处理LBK:连End物件一起复制了
                    case LaneBlockArea _lbk:
                        _lbk.CopyEntire((LaneBlockArea)source);
                        break;
                    //特殊处理SFL:连End物件一起复制了
                    case Soflan _sfl:
                        _sfl.CopyEntire((Soflan)source);
                        break;
                    //特殊处理Hold:清除Id
                    case Hold hold:
                        hold.ReferenceLaneStart = default;
                        undo += () => hold.ReferenceLaneStart = default;
                        break;
                    case Flick flick:
                        if (mirrorOption == PasteMirrorOption.XGridZeroMirror
                            || mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
                        {
                            var beforeDirection = flick.Direction;
                            redo += () => flick.Direction = (FlickDirection)(-(int)beforeDirection);
                            undo += () => flick.Direction = beforeDirection;
                        }
                        break;
                    default:
                        break;
                }

                TGrid newTGrid = default;
                if (copied is ITimelineObject timelineObject)
                {
                    var tGrid = timelineObject.TGrid.CopyNew();

                    double CalcY(double sourceEditorY)
                    {
                        var fixedY = adjustY(sourceEditorY);

                        var mirrorBaseY = mirrorYOpt is double _mirrorY ? _mirrorY : fixedY;
                        var mirroredY = mirrorBaseY + mirrorBaseY - fixedY;
                        var offsetedY = mirroredY + offset.Y;

                        return offsetedY;
                    }

                    var newY = CalcY(sourceCanvasPos.Y);

                    if (TGridCalculator.ConvertYToTGrid_DesignMode(newY, targetEditor) is not TGrid nt)
                    {
                        //todo warn
                        return;
                    }

                    newTGrid = nt;
                    redo += () => timelineObject.TGrid = newTGrid.CopyNew();
                    undo += () => timelineObject.TGrid = tGrid.CopyNew();

                    switch (copied)
                    {
                        case Soflan or LaneBlockArea:
                            ITimelineObject endIndicator = copied switch
                            {
                                Soflan _sfl => _sfl.EndIndicator,
                                LaneBlockArea _lbk => _lbk.EndIndicator,
                                _ => throw new Exception("这都能炸真的牛皮")
                            };
                            var oldEndIndicatorTGrid = endIndicator.TGrid.CopyNew();
                            var endIndicatorY = TGridCalculator.ConvertTGridToY_DesignMode(oldEndIndicatorTGrid, sourceEditor);
                            var newEndIndicatorY = CalcY(endIndicatorY);

                            if (TGridCalculator.ConvertYToTGrid_DesignMode(newEndIndicatorY, targetEditor) is not TGrid newEndIndicatorTGrid)
                            {
                                //todo warn
                                return;
                            }

                            redo += () => endIndicator.TGrid = newEndIndicatorTGrid.CopyNew();
                            undo += () => endIndicator.TGrid = oldEndIndicatorTGrid.CopyNew();

                            break;
                        case ConnectableStartObject start:
                            //apply child objects
                            foreach (var child in start.Children)
                            {
                                var oldChildTGrid = child.TGrid.CopyNew();
                                var y = TGridCalculator.ConvertTGridToY_DesignMode(oldChildTGrid, sourceEditor);
                                var newChildY = CalcY(y);

                                if (TGridCalculator.ConvertYToTGrid_DesignMode(newChildY, targetEditor) is not TGrid newChildTGrid)
                                {
                                    //todo warn
                                    return;
                                }

                                redo += () => child.TGrid = newChildTGrid.CopyNew();
                                undo += () => child.TGrid = oldChildTGrid.CopyNew();

                                foreach (var control in child.PathControls)
                                {
                                    var oldControlTGrid = control.TGrid.CopyNew();
                                    var cy = TGridCalculator.ConvertTGridToY_DesignMode(oldControlTGrid, sourceEditor);
                                    var newControlY = CalcY(cy);

                                    if (TGridCalculator.ConvertYToTGrid_DesignMode(newControlY, targetEditor) is not TGrid newControlTGrid)
                                    {
                                        //todo warn
                                        return;
                                    }

                                    redo += () => control.TGrid = newControlTGrid.CopyNew();
                                    undo += () => control.TGrid = oldControlTGrid.CopyNew();
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }

                XGrid newXGrid = default;
                var offsetedX = 0d; //后面会用到,因此提出来
                if (copied is IHorizonPositionObject horizonPositionObject)
                {
                    var xGrid = horizonPositionObject.XGrid.CopyNew();

                    double CalcX(double x)
                    {
                        var mirrorBaseX = mirrorXOpt is double _mirrorX ? _mirrorX : x;
                        var mirroredX = mirrorBaseX + mirrorBaseX - x;
                        offsetedX = mirroredX + offset.X;

                        return offsetedX;
                    }

                    var newX = CalcX(sourceCanvasPos.X);

                    if (XGridCalculator.ConvertXToXGrid(offsetedX, targetEditor) is not XGrid nx)
                    {
                        //todo warn
                        return;
                    }

                    newXGrid = nx;
                    undo += () => horizonPositionObject.XGrid = xGrid.CopyNew();
                    redo += () => horizonPositionObject.XGrid = newXGrid.CopyNew();

                    //apply child objects
                    if (copied is ConnectableStartObject start)
                    {
                        foreach (var child in start.Children)
                        {
                            var oldChildXGrid = child.XGrid.CopyNew();
                            var x = XGridCalculator.ConvertXGridToX(oldChildXGrid, targetEditor);
                            var newChildX = CalcX(x);

                            if (XGridCalculator.ConvertXToXGrid(newChildX, targetEditor) is not XGrid newChildXGrid)
                            {
                                //todo warn
                                return;
                            }

                            redo += () => child.XGrid = newChildXGrid.CopyNew();
                            undo += () => child.XGrid = oldChildXGrid.CopyNew();

                            foreach (var control in child.PathControls)
                            {
                                var oldControlXGrid = control.XGrid.CopyNew();
                                var cx = XGridCalculator.ConvertXGridToX(oldControlXGrid, targetEditor);
                                var newControlX = CalcX(cx);


                                if (XGridCalculator.ConvertXToXGrid(newControlX, targetEditor) is not XGrid newControlXGrid)
                                {
                                    //todo warn
                                    return;
                                }

                                redo += () => control.XGrid = newControlXGrid.CopyNew();
                                undo += () => control.XGrid = oldControlXGrid.CopyNew();
                            }
                        }
                    }
                }

                if (copied is IBulletPalleteReferencable bullet && bullet.ReferenceBulletPallete is BulletPallete pallete)
                {
                    //如果IsAppend为false,那就直接改引用直接成这个。否则就新建一个
                    var isAppend = false;
                    BulletPallete existPallete = default;
                    if (targetEditor.Fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == pallete.StrID) is BulletPallete e)
                    {
                        existPallete = e;
                        bool _check<T>(Func<BulletPallete, T> select)
                            => isAppend = isAppend || (Comparer<T>.Default.Compare(select(pallete), select(existPallete)) != 0);

                        _check(x => x.TypeValue);
                        _check(x => x.TargetValue);
                        _check(x => x.ShooterValue);
                        _check(x => x.EditorName);
                        //_check(x => x.EditorAxuiliaryLineColor);
                        _check(x => x.PlaceOffset);
                        _check(x => x.SizeValue);
                        //_check(x => x.Tag);
                    }
                    else
                        isAppend = true;

                    BulletPallete pickPallete = default;
                    if (isAppend)
                    {
                        var newPallete = pallete.CopyNew() as BulletPallete;
                        newPallete.EditorName = $"{(string.IsNullOrWhiteSpace(newPallete.EditorName) ? newPallete.StrID : newPallete.EditorName)} - 副本";
                        pickPallete = newPallete;
                    }
                    else
                    {
                        pickPallete = existPallete;
                    }

                    redo += () =>
                    {
                        if (isAppend)
                            targetEditor.Fumen.AddObject(pickPallete);
                        bullet.ReferenceBulletPallete = pickPallete;
                    };
                    undo += () =>
                    {
                        if (isAppend)
                            targetEditor.Fumen.RemoveObject(pickPallete);
                        bullet.ReferenceBulletPallete = default;
                    };
                }

                if (copied is ILaneDockable dockable)
                {
                    var before = dockable.ReferenceLaneStart;

                    redo += () =>
                    {
                        //这里做个检查吧:如果复制新的位置刚好也(靠近)在原来附着的轨道上，那就不变，否则就得清除ReferenceLaneStart
                        //todo 后面可以做更细节的检查和变动
                        if (dockable.ReferenceLaneStart is LaneStartBase beforeStart)
                        {
                            var needRedockLane = true;
                            if (beforeStart.CalulateXGrid(newTGrid) is XGrid xGrid)
                            {
                                var x = XGridCalculator.ConvertXGridToX(xGrid, targetEditor);
                                var diff = offsetedX - x;

                                if (Math.Abs(diff) < 8)
                                {
                                    //那就是在轨道上，不用动了！
                                    needRedockLane = false;
                                }
                                else
                                {
                                    dockable.ReferenceLaneStart = default;
                                }
                            }

                            if (needRedockLane)
                            {
                                var dockableLanes = targetEditor.Fumen.Lanes
                                    .GetVisibleStartObjects(newTGrid, newTGrid)
                                    .Where(x => x.IsDockableLane && x != beforeStart)
                                    .OrderBy(x => Math.Abs(x.LaneType - beforeStart.LaneType));

                                var pickLane = dockableLanes.FirstOrDefault();

                                //不在轨道上，那就清除惹
                                //todo 重新钦定一个轨道
                                dockable.ReferenceLaneStart = pickLane;
                            }
                        }
                        else
                            dockable.ReferenceLaneStart = default;
                    };

                    undo += () => dockable.ReferenceLaneStart = before;
                }

                var map = new Dictionary<ISelectableObject, bool>();
                foreach (var selectObj in ((copied as IDisplayableObject)?.GetDisplayableObjects() ?? Enumerable.Empty<IDisplayableObject>()).OfType<ISelectableObject>())
                    map[selectObj] = selectObj.IsSelected;

                redo += () =>
                {
                    targetEditor.Fumen.AddObject(copied);
                    foreach (var selectObj in map.Keys)
                        selectObj.IsSelected = true;
                };

                undo += () =>
                {
                    targetEditor.RemoveObject(copied);
                    foreach (var pair in map)
                        pair.Key.IsSelected = pair.Value;
                };
            };

            redo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
            undo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);

            targetEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("复制粘贴", redo, undo));
        }

        private double? CalculateYMirror(IEnumerable<OngekiObjectBase> objects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption != PasteMirrorOption.SelectedRangeCenterTGridMirror)
                return null;

            (var minY, var maxY) = objects
                    .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
                    .Where(x => x.Item1)
                    .Select(x => x.Y)
                    .MaxMinBy(x => x);

            var diffY = maxY - minY;
            var mirrorY = minY + diffY / 2f;
            return mirrorY;
        }

        private double? CalculateXMirror(FumenVisualEditorViewModel targetEditor, IEnumerable<OngekiObjectBase> objects, PasteMirrorOption mirrorOption)
        {
            if (mirrorOption == PasteMirrorOption.XGridZeroMirror)
                return XGridCalculator.ConvertXGridToX(0, targetEditor);

            if (mirrorOption == PasteMirrorOption.SelectedRangeCenterXGridMirror)
            {
                (var minX, var maxX) = objects
                    .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
                    .Where(x => x.Item1)
                    .Select(x => x.X)
                    .MaxMinBy(x => x);

                var diffX = maxX - minX;
                var mirrorX = minX + diffX / 2f;
                return mirrorX;
            }

            return null;
        }

        private Point CalculateRangeCenter(IEnumerable<OngekiObjectBase> objects)
        {
            (var minX, var maxX) = objects
                    .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.X) : default)
                    .Where(x => x.Item1)
                    .Select(x => x.X)
                    .MaxMinBy(x => x);

            var diffX = maxX - minX;
            var x = minX + diffX / 2f;

            (var minY, var maxY) = objects
                    .Select(x => currentCopiedSources.TryGetValue(x, out var p) ? (true, p.Y) : default)
                    .Where(x => x.Item1)
                    .Select(x => x.Y)
                    .MaxMinBy(x => x);

            var diffY = maxY - minY;
            var y = minY + diffY / 2f;

            return new(x, y);
        }
    }
}
