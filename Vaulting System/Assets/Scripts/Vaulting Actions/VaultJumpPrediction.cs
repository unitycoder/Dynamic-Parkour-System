﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Climbing
{
    public class VaultJumpPrediction : VaultAction
    {
        JumpPredictionController jumpController;


        public VaultJumpPrediction(VaultingController _vaultingController) : base(_vaultingController)
        {
            jumpController = vaultingController.controller.GetComponent<JumpPredictionController>();
        }

        public override bool CheckAction()
        {
            jumpController.curPoint = null;
            jumpController.JumpUpdate();

            return !jumpController.hasArrived();
        }

        public override bool ExecuteAction()
        {
            return jumpController.ExecuteFollow();
        }
    }
}