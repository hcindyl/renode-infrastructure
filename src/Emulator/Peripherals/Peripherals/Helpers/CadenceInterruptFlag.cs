//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.Helpers
{
    public class CadenceInterruptFlag
    {
        public CadenceInterruptFlag(Func<bool> statusProvider)
        {
            this.statusProvider = statusProvider;
        }

        public void UpdateStickyStatus()
        {
            StickyStatus = StickyStatus || Status;
        }

        public void SetSticky(bool set)
        {
            StickyStatus |= set;
        }

        public void ClearSticky(bool clear)
        {
            StickyStatus &= !clear;
        }

        public void InterruptEnable(bool enable)
        {
            InterruptMask |= enable;
        }

        public void InterruptDisable(bool disable)
        {
            InterruptMask &= !disable;
        }

        public bool Status
        {
            get => statusProvider();
        }

        public bool StickyStatus { get; private set; }

        public bool InterruptMask { get; private set; }

        public bool InterruptStatus
        {
            get => StickyStatus && InterruptMask;
        }

        private readonly Func<bool> statusProvider;
    }
}
