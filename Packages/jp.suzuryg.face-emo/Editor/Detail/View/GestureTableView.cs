﻿using Suzuryg.FaceEmo.Domain;
using Suzuryg.FaceEmo.UseCase;
using Suzuryg.FaceEmo.Detail.Drawing;
using Suzuryg.FaceEmo.Detail.Localization;
using Suzuryg.FaceEmo.Detail.View.Element;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UniRx;
using Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode;
using System.Linq;

namespace Suzuryg.FaceEmo.Detail.View
{
    public class GestureTableView : IDisposable
    {
        private IAddBranchUseCase _addBranchUseCase;

        private IReadOnlyLocalizationSetting _localizationSetting;
        private ISubWindowProvider _subWindowProvider;
        private DefaultsProviderGenerator _defaultProviderGenerator;
        private UpdateMenuSubject _updateMenuSubject;
        private SelectionSynchronizer _selectionSynchronizer;
        private GestureTableThumbnailDrawer _thumbnailDrawer;
        private SerializedObject _thumbnailSetting;

        private GestureTableElement _gestureTableElement;

        private IMGUIContainer _gestureTableContainer;
        private Label _thumbnailWidthLabel;
        private Label _thumbnailHeightLabel;
        private SliderInt _thumbnailWidthSlider;
        private SliderInt _thumbnailHeightSlider;
        private Button _addBranchButton;

        private StyleColor _canAddButtonColor = Color.black;
        private StyleColor _canAddButtonBackgroundColor = Color.yellow;
        private StyleColor _canNotAddButtonColor;
        private StyleColor _canNotAddButtonBackgroundColor;

        private CompositeDisposable _disposables = new CompositeDisposable();

        public GestureTableView(
            IAddBranchUseCase addBranchUseCase,
            IReadOnlyLocalizationSetting localizationSetting,
            ISubWindowProvider subWindowProvider,
            DefaultsProviderGenerator defaultProviderGenerator,
            UpdateMenuSubject updateMenuSubject,
            SelectionSynchronizer selectionSynchronizer,
            GestureTableThumbnailDrawer thumbnailDrawer,
            GestureTableElement gestureTableElement,
            ThumbnailSetting thumbnailSetting)
        {
            // Usecases
            _addBranchUseCase = addBranchUseCase;

            // Others
            _localizationSetting = localizationSetting;
            _subWindowProvider = subWindowProvider;
            _defaultProviderGenerator = defaultProviderGenerator;
            _updateMenuSubject = updateMenuSubject;
            _selectionSynchronizer = selectionSynchronizer;
            _thumbnailDrawer = thumbnailDrawer;
            _gestureTableElement = gestureTableElement;
            _thumbnailSetting = new SerializedObject(thumbnailSetting);

            // Gesture table element
            _gestureTableElement.AddTo(_disposables);
            _gestureTableElement.OnSelectionChanged.Synchronize().Subscribe(OnSelectionChanged).AddTo(_disposables);
            _gestureTableElement.OnBranchIndexExceeded.Synchronize().Subscribe(_ => OnBranchIndexExceeded()).AddTo(_disposables);

            // Localization table changed event handler
            _localizationSetting.OnTableChanged.Synchronize().Subscribe(SetText).AddTo(_disposables);

            // Update menu event handler
            _updateMenuSubject.Observable.Synchronize().Subscribe(OnMenuUpdated).AddTo(_disposables);

            // Synchronize selection event handler
            _selectionSynchronizer.OnSynchronizeSelection.Synchronize().Subscribe(OnSynchronizeSelection).AddTo(_disposables);
        }

        public void Dispose()
        {
            _thumbnailWidthSlider.UnregisterValueChangedCallback(OnThumbnailSizeChanged);
            _thumbnailHeightSlider.UnregisterValueChangedCallback(OnThumbnailSizeChanged);
            _disposables.Dispose();
        }

        public void Initialize(VisualElement root)
        {
            // Load UXML and style
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{DetailConstants.ViewDirectory}/{nameof(GestureTableView)}.uxml");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{DetailConstants.ViewDirectory}/{nameof(GestureTableView)}.uss");
            NullChecker.Check(uxml, style);

            root.styleSheets.Add(style);
            uxml.CloneTree(root);

            // Query Elements
            _gestureTableContainer = root.Q<IMGUIContainer>("GestureTableContainer");
            _thumbnailWidthLabel = root.Q<Label>("ThumbnailWidthLabel");
            _thumbnailHeightLabel = root.Q<Label>("ThumbnailHeightLabel");
            _thumbnailWidthSlider = root.Q<SliderInt>("ThumbnailWidthSlider");
            _thumbnailHeightSlider = root.Q<SliderInt>("ThumbnailHeightSlider");
            _addBranchButton = root.Q<Button>("AddBranchButton");
            NullChecker.Check(_gestureTableContainer, _thumbnailWidthLabel, _thumbnailHeightLabel, _thumbnailWidthSlider, _thumbnailHeightSlider, _addBranchButton);

            // Add event handlers
            Observable.FromEvent(x => _addBranchButton.clicked += x, x => _addBranchButton.clicked -= x)
                .Synchronize().Subscribe(_ => OnAddBranchButtonClicked()).AddTo(_disposables);
            Observable.FromEvent(x => _gestureTableContainer.onGUIHandler += x, x => _gestureTableContainer.onGUIHandler -= x)
                .Synchronize().Subscribe(_ =>
                {
                    _gestureTableElement?.OnGUI(_gestureTableContainer.contentRect);

                    // To draw gesture cell selection
                    if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDown)
                    {
                        _gestureTableContainer.MarkDirtyRepaint();
                    }
                }).AddTo(_disposables);

            // Initialize fields
            _thumbnailSetting.Update();

            _thumbnailWidthSlider.bindingPath = nameof(ThumbnailSetting.GestureTable_Width);
            _thumbnailWidthSlider.BindProperty(_thumbnailSetting);
            _thumbnailWidthSlider.lowValue = ThumbnailSetting.GestureTable_MinWidth;
            _thumbnailWidthSlider.highValue = ThumbnailSetting.GestureTable_MaxWidth;
            _thumbnailWidthSlider.value = _thumbnailSetting.FindProperty(nameof(ThumbnailSetting.GestureTable_Width)).intValue;

            _thumbnailHeightSlider.bindingPath = nameof(ThumbnailSetting.GestureTable_Height);
            _thumbnailHeightSlider.BindProperty(_thumbnailSetting);
            _thumbnailHeightSlider.lowValue = ThumbnailSetting.GestureTable_MinHeight;
            _thumbnailHeightSlider.highValue = ThumbnailSetting.GestureTable_MaxHeight;
            _thumbnailHeightSlider.value = _thumbnailSetting.FindProperty(nameof(ThumbnailSetting.GestureTable_Height)).intValue;

            // Initialize styles
            _canNotAddButtonColor = _addBranchButton.style.color;
            _canNotAddButtonBackgroundColor = _addBranchButton.style.backgroundColor;

            // Add event handlers
            _thumbnailWidthSlider.RegisterValueChangedCallback(OnThumbnailSizeChanged);
            _thumbnailHeightSlider.RegisterValueChangedCallback(OnThumbnailSizeChanged);

            // Set text
            SetText(_localizationSetting.Table);
        }

        private void SetText(LocalizationTable localizationTable)
        {
            if (_addBranchButton != null) { _addBranchButton.text = localizationTable.GestureTableView_AddBranch; }
            if (_thumbnailWidthLabel != null) { _thumbnailWidthLabel.text = localizationTable.Common_ThumbnailWidth; }
            if (_thumbnailHeightLabel != null) { _thumbnailHeightLabel.text = localizationTable.Common_ThumbnailHeight; }
        }

        private void OnMenuUpdated(IMenu menu)
        {
            _gestureTableElement.Setup(menu);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var target = GetTarget();
            var canAddBranch = target.canAddBranch;

            _addBranchButton?.SetEnabled(canAddBranch);

            if (_addBranchButton != null)
            {
                if (canAddBranch)
                {
                    _addBranchButton.style.color = _canAddButtonColor;
                    _addBranchButton.style.backgroundColor = _canAddButtonBackgroundColor;
                }
                else
                {
                    _addBranchButton.style.color = _canNotAddButtonColor;
                    _addBranchButton.style.backgroundColor = _canNotAddButtonBackgroundColor;
                }
            }


            _gestureTableContainer?.MarkDirtyRepaint();
        }

        private void OnSelectionChanged((HandGesture left, HandGesture right)? args)
        {
            if (!(args is null))
            {
                _selectionSynchronizer.ChangeGestureTableViewSelection(args.Value.left, args.Value.right);
            }
        }

        private void OnBranchIndexExceeded()
        {
            var menu = _gestureTableElement.Menu;
            if (menu is null || !menu.ContainsMode(_gestureTableElement.SelectedModeId))
            {
                return;
            }

            var mode = menu.GetMode(_gestureTableElement.SelectedModeId);
            var lastBranchIndex = mode.Branches.Count - 1;
            _selectionSynchronizer.ChangeBranchListViewSelection(lastBranchIndex);
        }

        private void OnSynchronizeSelection(ViewSelection viewSelection)
        {
            _gestureTableElement?.ChangeSelection(viewSelection.MenuItemListView, viewSelection.BranchListView, viewSelection.GestureTableView);
            UpdateDisplay();
        }

        private void OnThumbnailSizeChanged(ChangeEvent<int> changeEvent)
        {
            // TODO: Reduce unnecessary redrawing
            _thumbnailDrawer.ClearCache();
        }

        private void OnAddBranchButtonClicked()
        {
            var target = GetTarget();

            var conditions = new[]
            {
                new Condition(Hand.Left, target.left, ComparisonOperator.Equals),
                new Condition(Hand.Right, target.right, ComparisonOperator.Equals),
            };
            _addBranchUseCase.Handle("", _gestureTableElement.SelectedModeId,
                conditions: conditions,
                defaultsProvider: _defaultProviderGenerator.Generate());
        }

        private (bool canAddBranch, HandGesture left, HandGesture right) GetTarget()
        {
            var canNot = (false, HandGesture.Neutral, HandGesture.Neutral);

            if (_gestureTableElement.SelectedCell is null)
            {
                return canNot;
            }
            (var left, var right) = _gestureTableElement.SelectedCell.Value;

            var menu = _gestureTableElement.Menu;
            if (menu is null)
            {
                return canNot;
            }

            if (!menu.ContainsMode(_gestureTableElement.SelectedModeId))
            {
                return canNot;
            }

            var mode = menu.GetMode(_gestureTableElement.SelectedModeId);
            if (mode.GetGestureCell(left, right) is IBranch)
            {
                return canNot;
            }
            else
            {
                return (true, left, right);
            }
        }
    }
}