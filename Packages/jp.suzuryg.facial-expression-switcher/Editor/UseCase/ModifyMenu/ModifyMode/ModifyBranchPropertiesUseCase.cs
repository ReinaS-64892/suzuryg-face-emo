﻿using Suzuryg.FacialExpressionSwitcher.Domain;
using System;
using System.Collections.Generic;

namespace Suzuryg.FacialExpressionSwitcher.UseCase.ModifyMenu.ModifyMode
{
    public interface IModifyBranchPropertiesUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex,
            EyeTrackingControl? eyeTrackingControl = null,
            MouthTrackingControl? mouthTrackingControl = null,
            bool? isLeftTriggerUsed = null,
            bool? isRightTriggerUsed = null);
    }

    public interface IModifyBranchPropertiesPresenter
    {
        event Action<ModifyBranchPropertiesResult, IMenu, string> OnCompleted;

        void Complete(ModifyBranchPropertiesResult modifyBranchPropertiesResult, in IMenu menu, string errorMessage = "");
    }

    public enum ModifyBranchPropertiesResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidBranch,
        ArgumentNull,
        Error,
    }

    public class ModifyBranchPropertiesPresenter : IModifyBranchPropertiesPresenter
    {
        public event Action<ModifyBranchPropertiesResult, IMenu, string> OnCompleted;

        public void Complete(ModifyBranchPropertiesResult modifyBranchPropertiesResult, in IMenu menu, string errorMessage = "")
        {
            OnCompleted(modifyBranchPropertiesResult, menu, errorMessage);
        }
    }

    public class ModifyBranchPropertiesUseCase : IModifyBranchPropertiesUseCase
    {
        IMenuRepository _menuRepository;
        IModifyBranchPropertiesPresenter _modifyBranchPropertiesPresenter;

        public ModifyBranchPropertiesUseCase(IMenuRepository menuRepository, IModifyBranchPropertiesPresenter modifyBranchPropertiesPresenter)
        {
            _menuRepository = menuRepository;
            _modifyBranchPropertiesPresenter = modifyBranchPropertiesPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex,
            EyeTrackingControl? eyeTrackingControl = null,
            MouthTrackingControl? mouthTrackingControl = null,
            bool? isLeftTriggerUsed = null,
            bool? isRightTriggerUsed = null)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _modifyBranchPropertiesPresenter.Complete(ModifyBranchPropertiesResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _modifyBranchPropertiesPresenter.Complete(ModifyBranchPropertiesResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanModifyBranchProperties(modeId, branchIndex))
                {
                    _modifyBranchPropertiesPresenter.Complete(ModifyBranchPropertiesResult.InvalidBranch, menu);
                    return;
                }

                menu.ModifyBranchProperties(modeId, branchIndex, eyeTrackingControl, mouthTrackingControl, isLeftTriggerUsed, isRightTriggerUsed);

                _menuRepository.Save(menuId, menu, "ModifyBranchProperties");
                _modifyBranchPropertiesPresenter.Complete(ModifyBranchPropertiesResult.Succeeded, menu);
            }
            catch (Exception ex)
            {
                _modifyBranchPropertiesPresenter.Complete(ModifyBranchPropertiesResult.Error, null, ex.ToString());
            }
        }
    }
}
