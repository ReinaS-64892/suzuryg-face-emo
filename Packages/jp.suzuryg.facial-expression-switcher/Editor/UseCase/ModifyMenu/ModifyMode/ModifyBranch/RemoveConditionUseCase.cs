﻿using Suzuryg.FacialExpressionSwitcher.Domain;
using System;
using System.Collections.Generic;
using UniRx;

namespace Suzuryg.FacialExpressionSwitcher.UseCase.ModifyMenu.ModifyMode.ModifyBranch
{
    public interface IRemoveConditionUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex, int conditionIndex);
    }

    public interface IRemoveConditionPresenter
    {
        IObservable<(RemoveConditionResult, IMenu, string)> Observable { get; }

        void Complete(RemoveConditionResult removeConditionResult, in IMenu menu, string errorMessage = "");
    }

    public enum RemoveConditionResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidCondition,
        ArgumentNull,
        Error,
    }

    public class RemoveConditionPresenter : IRemoveConditionPresenter
    {
        public IObservable<(RemoveConditionResult, IMenu, string)> Observable => _subject.AsObservable().Synchronize();

        private Subject<(RemoveConditionResult, IMenu, string)> _subject = new Subject<(RemoveConditionResult, IMenu, string)>();

        public void Complete(RemoveConditionResult removeConditionResult, in IMenu menu, string errorMessage = "")
        {
            _subject.OnNext((removeConditionResult, menu, errorMessage));
        }
    }

    public class RemoveConditionUseCase : IRemoveConditionUseCase
    {
        IMenuRepository _menuRepository;
        IRemoveConditionPresenter _removeConditionPresenter;

        public RemoveConditionUseCase(IMenuRepository menuRepository, IRemoveConditionPresenter removeConditionPresenter)
        {
            _menuRepository = menuRepository;
            _removeConditionPresenter = removeConditionPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex, int conditionIndex)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _removeConditionPresenter.Complete(RemoveConditionResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _removeConditionPresenter.Complete(RemoveConditionResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanRemoveCondition(modeId, branchIndex, conditionIndex))
                {
                    _removeConditionPresenter.Complete(RemoveConditionResult.InvalidCondition, menu);
                    return;
                }

                menu.RemoveCondition(modeId, branchIndex, conditionIndex);

                _menuRepository.Save(menuId, menu, "RemoveCondition");
                _removeConditionPresenter.Complete(RemoveConditionResult.Succeeded, menu);
            }
            catch (Exception ex)
            {
                _removeConditionPresenter.Complete(RemoveConditionResult.Error, null, ex.ToString());
            }
        }
    }
}
