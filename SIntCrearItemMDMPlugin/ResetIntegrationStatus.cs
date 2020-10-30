using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Xeno.Data;
using Xeno.LinearWorkflow;
using Xeno.Prodika.GSMLib.Workflow;
using Xeno.Prodika.Services;

namespace SIntCrearItemMDMPlugin
{
    public class ResetIntegrationStatus : SimpleLinearWorkflowActionBase
    {
        public override void Execute(ILinearTransitionContext ctx)
        {
            SpecLinearTransitionContext transitionContext = (SpecLinearTransitionContext)ctx;
            ISpecificationService specService = transitionContext.SpecificationService;
            IBaseSpec spec = (IBaseSpec)specService.Current;
            Utils.SaveIntegrationStatus(spec.PKID, spec.SpecSummary.WorkflowStatus.Status, null);
        }
    }
}