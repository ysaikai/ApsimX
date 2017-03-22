﻿using Models.Core;
using Models.WholeFarm.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Models.WholeFarm.Activities
{
	/// <summary>Ruminant graze activity</summary>
	/// <summary>This activity determines how a ruminant group will graze</summary>
	/// <summary>It is designed to request food via a food store arbitrator</summary>
	/// <version>1.0</version>
	/// <updates>1.0 First implementation of this activity using NABSA processes</updates>
	[Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
	[ValidParent(ParentType = typeof(WFActivityBase))]
	[ValidParent(ParentType = typeof(ActivitiesHolder))]
	public class RuminantActivityGrazeAll : WFActivityBase
	{
		[Link]
		private ResourcesHolder Resources = null;

		/// <summary>An event handler to allow us to initialise ourselves.</summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
		[EventSubscribe("Commencing")]
		private void OnSimulationCommencing(object sender, EventArgs e)
		{
			// create activity for each pasture type and breed at startup
			foreach (GrazeFoodStoreType pastureType in Resources.GrazeFoodStore().Children)
			{
				RuminantActivityGrazePasture ragp = new RuminantActivityGrazePasture();
				ragp.GrazeFoodStoreModel = pastureType;

				foreach (RuminantType herdType in Resources.RuminantHerd().Children)
				{
					RuminantActivityGrazePastureBreed ragpb = new RuminantActivityGrazePastureBreed();
					ragpb.GrazeFoodStoreModel = pastureType;
					ragpb.RuminantTypeModel = herdType;
					ragp.ActivityList.Add(ragpb);
				}
				ActivityList.Add(ragp);
			}
		}

		/// <summary>
		/// Method to determine resources required for this activity in the current month
		/// </summary>
		/// <returns>List of required resource requests</returns>
		public override List<ResourceRequest> DetermineResourcesNeeded()
		{
			return null;
		}

		/// <summary>
		/// Method used to perform activity if it can occur as soon as resources are available.
		/// </summary>
		public override void PerformActivity()
		{
			return;
		}
	}
}
