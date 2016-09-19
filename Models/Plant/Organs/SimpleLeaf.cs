using System;
using System.Collections.Generic;
using System.Text;

using Models.Core;
using Models.PMF.Functions;
using Models.PMF.Functions.SupplyFunctions;
using System.Xml.Serialization;
using Models.PMF.Interfaces;
using Models.Interfaces;
using Models.PMF.Phen;
using APSIM.Shared.Utilities;

namespace Models.PMF.Organs
{
    /// <summary>
    /// This plant organ is parameterised using a simple leaf organ type which provides the core functions of intercepting radiation, providing a photosynthesis supply and a transpiration demand.  It is parameterised as follows.
    /// 
    /// **Dry Matter Supply**
    /// 
    /// DryMatter Fixation Supply (Photosynthesis) provided to the Organ Arbitrator (for partitioning between organs) is calculated each day as the product of a unstressed potential and a series of stress factors.
    /// DM is not retranslocated out of this organ.
    /// 
    /// **Dry Matter Demands**
    /// 
    /// A given fraction of daily DM demand is determined to be structural and the remainder is non-structural.
    /// 
    /// **Nitrogen Demands**
    /// 
    /// The daily structural N demand of this organ is the product of Total DM demand and a maximum Nitrogen concentration.  The Nitrogen demand switch is a multiplier applied to nitrogen demand so it can be turned off at certain phases.
    /// 
    /// **Nitrogen Supplies**
    /// 
    /// N is not reallocated from  this organ.  Nonstructural N is not available for retranslocation to other organs.
    /// 
    /// **Biomass Senescence and Detachment**
    /// 
    /// No senescence occurs from this organ.  
    /// No detachment occurs from this organ.
    /// 
    /// **Canopy**
    /// 
    /// The user can model the canopy by specifying either the LAI and an extinction coefficient, or by specifying the canopy cover directly.  If the cover is specified, LAI is calculated using an inverted Beer-Lambert equation with the specified cover value.
    /// 
    /// The canopies values of Cover and LAI are passed to the MicroClimate module which uses the Penman Monteith equation to calculate potential evapotranspiration for each canopy and passes the value back to the crop.
    /// The effect of growth rate on transpiration is captured using the Fractional Growth Rate (FRGR) function which is parameterised as a function of temperature for the simple leaf. 
    ///
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class SimpleLeaf : GenericOrgan, ICanopy, ILeaf
    {
        #region Leaf Interface
        /// <summary>
        /// 
        /// </summary>
        public bool CohortsInitialised { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int TipsAtEmergence { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int CohortsAtInitialisation { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double InitialisedCohortNo { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double AppearedCohortNo { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public double PlantAppearedLeafNo { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="proprtionRemoved"></param>
        public void DoThin(double proprtionRemoved) { }
        #endregion

        #region Canopy interface

        /// <summary>Gets the canopy. Should return null if no canopy present.</summary>
        public string CanopyType { get { return Plant.CropType; } }

        /// <summary>Albedo.</summary>
        [Description("Albedo")]
        public double Albedo { get; set; }

        /// <summary>Gets or sets the gsmax.</summary>
        [Description("GSMAX")]
        public double Gsmax { get; set; }

        /// <summary>Gets or sets the R50.</summary>
        [Description("R50")]
        public double R50 { get; set; }

        /// <summary>Gets the LAI</summary>
        [Units("m^2/m^2")]
        public double LAI { get; set; }

        /// <summary>Gets the LAI live + dead (m^2/m^2)</summary>
        public double LAITotal { get { return LAI + LAIDead; } }

        /// <summary>Gets the cover green.</summary>
        [Units("0-1")]
        public double CoverGreen
        {
            get
            {
                if (Plant.IsAlive)
                {
                    double greenCover = 0.0;
                    if (CoverFunction == null)
                        greenCover = 1.0 - Math.Exp(-ExtinctionCoefficientFunction.Value * LAI);
                    else
                        greenCover = CoverFunction.Value;
                    return Math.Min(Math.Max(greenCover, 0.0), 0.999999999); // limiting to within 10^-9, so MicroClimate doesn't complain
                }
                else
                    return 0.0;

            }
        }

        /// <summary>Gets the cover total.</summary>
        [Units("0-1")]
        public double CoverTotal
        {
            get { return 1.0 - (1 - CoverGreen) * (1 - CoverDead); }
        }

        /// <summary>Gets or sets the height.</summary>
        [Units("mm")]
        public double Height { get; set; }
        /// <summary>Gets the depth.</summary>
        [Units("mm")]
        public double Depth { get { return Height; } }//  Fixme.  This needs to be replaced with something that give sensible numbers for tree crops

        /// <summary>Gets or sets the FRGR.</summary>
        [Units("mm")]
        public double FRGR { get; set; }

        /// <summary>Sets the potential evapotranspiration. Set by MICROCLIMATE.</summary>
        public double PotentialEP { get; set; }

        /// <summary>Sets the light profile. Set by MICROCLIMATE.</summary>
        public CanopyEnergyBalanceInterceptionlayerType[] LightProfile { get; set; }
        #endregion

        #region Parameters
        /// <summary>The FRGR function</summary>
        [Link]
        IFunction FRGRFunction = null;   // VPD effect on Growth Interpolation Set
        /// <summary>The dm demand function</summary>
        [Link]
        IFunction DMDemandFunction = null;
        /// <summary>The cover function</summary>
        [Link(IsOptional = true)]
        IFunction CoverFunction = null;
        /// <summary>The nitrogen demand switch</summary>
        [Link(IsOptional = true)]
        IFunction NitrogenDemandSwitch = null;

        /// <summary>The lai function</summary>
        [Link(IsOptional = true)]
        IFunction LAIFunction = null;
        /// <summary>The extinction coefficient function</summary>
        [Link(IsOptional = true)]
        IFunction ExtinctionCoefficientFunction = null;
        /// <summary>The photosynthesis</summary>
        [Link]
        IFunction Photosynthesis = null;
        /// <summary>The height function</summary>
        [Link]
        IFunction HeightFunction = null;
        /// <summary>The lai dead function</summary>
        [Link]
        IFunction LaiDeadFunction = null;
        /// <summary>The structural fraction</summary>
        [Link]
        IFunction StructuralFraction = null;

        /// <summary>The structure</summary>
        [Link(IsOptional = true)]
        public Structure Structure = null;
        /// <summary>The phenology</summary>
        [Link(IsOptional = true)]
        public Phenology Phenology = null;
        /// <summary>TE Function</summary>
        [Link(IsOptional = true)]
        IFunction TranspirationEfficiency = null;
        /// <summary></summary>
        [Link(IsOptional = true)]
        IFunction SVPFrac = null;

        #endregion

        #region States and variables

        /// <summary>Gets or sets the ep.</summary>
        /// <value>The ep.</value>
        private double EP { get; set; }
        /// <summary>Gets or sets the k dead.</summary>
        /// <value>The k dead.</value>
        public double KDead { get; set; }                  // Extinction Coefficient (Dead)
        /// <summary>Gets or sets the water demand.</summary>
        /// <value>The water demand.</value>
        [Units("mm")]
        public override double WaterDemand
        {
            get
            {
                if (SVPFrac != null && TranspirationEfficiency != null)
                {
                    double svpMax = MetUtilities.svp(MetData.MaxT) * 0.1;
                    double svpMin = MetUtilities.svp(MetData.MinT) * 0.1;
                    double vpd = Math.Max(SVPFrac.Value * (svpMax - svpMin), 0.01);

                    return Photosynthesis.Value / (TranspirationEfficiency.Value / vpd / 0.001);
                }
                return PotentialEP;
            }
        }
        /// <summary>Gets the transpiration.</summary>
        /// <value>The transpiration.</value>
        public double Transpiration { get { return EP; } }

        /// <summary>Gets the fw.</summary>
        /// <value>The fw.</value>
        public double Fw
        {
            get
            {
                if (WaterDemand > 0)
                    return EP / WaterDemand;
                else
                    return 1;
            }
        }
        /// <summary>Gets the function.</summary>
        /// <value>The function.</value>
        public double Fn
        {
            get
            {
                double MaxNContent = Live.Wt * MaximumNConc.Value;
                return Live.N / MaxNContent;
            }
        }

        /// <summary>Gets or sets the lai dead.</summary>
        /// <value>The lai dead.</value>
        public double LAIDead { get; set; }


        /// <summary>Gets the cover dead.</summary>
        /// <value>The cover dead.</value>
        public double CoverDead
        {
            get { return 1.0 - Math.Exp(-KDead * LAIDead); }
        }
        /// <summary>Gets the RAD int tot.</summary>
        /// <value>The RAD int tot.</value>
        [Units("MJ/m^2/day")]
        [Description("This is the intercepted radiation value that is passed to the RUE class to calculate DM supply")]
        public double RadIntTot
        {
            get
            {
                return CoverGreen * MetData.Radn;
            }
        }

        /// <summary>Flag whether leaf DM has been initialised</summary>
        private bool isInitialised = false;

        #endregion

        #region Arbitrator Methods
        /// <summary>Gets or sets the water allocation.</summary>
        /// <value>The water allocation.</value>
        public override double WaterAllocation { get { return EP; } set { EP += value; } }
        
        /// <summary>Gets or sets the dm demand.</summary>
        /// <value>The dm demand.</value>
        public override BiomassPoolType DMDemand
        {
            get
            {
                double Demand = DMDemandFunction.Value;
                if (Math.Round(Demand, 8) < 0)
                    throw new Exception(this.Name + " organ is returning a negative DM demand.  Check your parameterisation");
                return new BiomassPoolType { Structural = Demand };
            }
        }

        /// <summary>Gets or sets the dm supply.</summary>
        /// <value>The dm supply.</value>
        public override BiomassSupplyType DMSupply
        {
            get
            {
                if (Math.Round(Photosynthesis.Value + AvailableDMRetranslocation(), 8) < 0)
                    throw new Exception(this.Name + " organ is returning a negative DM supply.  Check your parameterisation");
                return new BiomassSupplyType
                {
                    Fixation = Photosynthesis.Value,
                    Retranslocation = AvailableDMRetranslocation(),
                    Reallocation = 0.0
                };
            }
        }

        /// <summary>Sets the dm allocation.</summary>
        /// <value>The dm allocation.</value>
        public override BiomassAllocationType DMAllocation
        {

            set
            {
                // What is going on here?  Why no non-structural???
                // This needs to be checked!
                Live.StructuralWt += value.Structural;
            }
        }
        /// <summary>Gets or sets the n demand.</summary>
        /// <value>The n demand.</value>
        public override BiomassPoolType NDemand
        {
            get
            {
                double StructuralDemand = 0;
                double NDeficit = 0;

                if (NitrogenDemandSwitch != null)
                    if (NitrogenDemandSwitch.Value == 0)
                        NDeficit = 0;

                StructuralDemand = MaximumNConc.Value * PotentialDMAllocation * StructuralFraction.Value;
                NDeficit = Math.Max(0.0, MaximumNConc.Value * (Live.Wt + PotentialDMAllocation) - Live.N) - StructuralDemand;
                
                if (Math.Round(StructuralDemand, 8) < 0)
                    throw new Exception(this.Name + " organ is returning a negative structural N Demand.  Check your parameterisation");
                if (Math.Round(NDeficit, 8) < 0)
                    throw new Exception(this.Name + " organ is returning a negative Non structural N Demand.  Check your parameterisation");
                return new BiomassPoolType { Structural = StructuralDemand, NonStructural = NDeficit };
            }
        }

        /// <summary>Sets the n allocation.</summary>
        /// <value>The n allocation.</value>
        /// <exception cref="System.Exception">
        /// Invalid allocation of N
        /// or
        /// N allocated to Leaf left over after allocation
        /// or
        /// UnKnown Leaf N allocation problem
        /// </exception>
        public override BiomassAllocationType NAllocation
        {
            set
            {
                Live.StructuralN += value.Structural;
                Live.NonStructuralN += value.NonStructural;
            }
        }

        #endregion

        #region Events

        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private new void OnSimulationCommencing(object sender, EventArgs e)
        {
            Clear();
        }

        /// <summary>Called when [do daily initialisation].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoDailyInitialisation")]
        protected override void OnDoDailyInitialisation(object sender, EventArgs e)
        {
            if (Phenology != null)
                if (Phenology.OnDayOf("Emergence"))
                   if (Structure != null)
                        Structure.LeafTipsAppeared = 1.0;
            EP = 0;
        }
        #endregion

        #region Component Process Functions

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="data">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantSowing")]
        private new void OnPlantSowing(object sender, SowPlant2Type data)
        {
            if (data.Plant == Plant)
                Clear();
        }

        /// <summary>Clears this instance.</summary>
        protected override void Clear()
        {
            base.Clear();
            Height = 0;
            LAI = 0;
        }
        #endregion

        #region Top Level time step functions
        /// <summary>Event from sequencer telling us to do our potential growth.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantGrowth")]
        private new void OnDoPotentialPlantGrowth(object sender, EventArgs e)
        {
            base.OnDoPotentialPlantGrowth(sender, e);
            if (Plant.IsEmerged)
            {
                if (!isInitialised)
                {
                    Live.StructuralWt = InitialWtFunction.Value;
                    Live.StructuralN = Live.StructuralWt * MaxNconc;
                    isInitialised = true;
                }

                FRGR = FRGRFunction.Value;
                if (CoverFunction == null & ExtinctionCoefficientFunction == null)
                    throw new Exception("\"CoverFunction\" or \"ExtinctionCoefficientFunction\" should be defined in " + this.Name);
                if (CoverFunction != null)
                    LAI = (Math.Log(1 - CoverGreen) / (ExtinctionCoefficientFunction.Value * -1));
                if (LAIFunction != null)
                    LAI = LAIFunction.Value;

                Height = HeightFunction.Value;

                LAIDead = LaiDeadFunction.Value;

            }
        }

        #endregion

    }
}
