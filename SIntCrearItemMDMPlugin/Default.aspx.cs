using SIntCrearItemMDMPlugin.SIntCrearItemMdmWS;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml.Serialization;
using System.Xml.XPath;
using Xeno.Data;
using Xeno.Data.Common;
using Xeno.Data.GSM;
using Xeno.Prodika.Application;
using Xeno.Prodika.Reflection;
using Xeno.Prodika.Services;
using Xeno.Prodika.Services.UOMService;

namespace SIntCrearItemMDMPlugin
{
    public partial class Default : Page
    {
        private string SKUSystemCode;
        private string ChampSystemCode;
        private string CategoriaSATAttrId;
        private string FechaLanzamientoAttrId;
        private string AlturaAttrId;
        private string AnchoAttrId;
        private string LongitudAttrId;
        private string PesoAttrId;
        private string RawMaterials;
        private string BulkCategory;
        private string CodProdTermAttrId;
        
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsCallback)
            {
                GetConstants();
                listMessages.DataSource = new List<string>();

                try
                {
                    CrearItemMDM(Request.QueryString["specId"]);
                }
                catch (CustomValidationException ex)
                {
                    listMessages.Items.Add(ex.Message);
                }
                catch (System.ServiceModel.CommunicationException ex)
                {
                    Logger.Log(ex.ToString());
                    listMessages.Items.Add("Error de comunicación con el servicio.");
                    listMessages.Items.Add(ex.Message);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.ToString());
                    listMessages.Items.Add("Error al enviar spec a MDM.");
                    listMessages.Items.Add(ex.Message);
                }
            }
        }

        private bool CrearItemMDM(string specId)
        {
            var isValid = true;
            var specService = Utils.GetService<ISpecificationService>();
            specService.Load(specId);
            var spec = (IBaseSpec)specService.Current;

            //Check if spec is a Material or a Package
            if (!(spec is IIngredientSpecification || spec is IPackagingSpecification))
            {
                throw new CustomValidationException(string.Format("Spec {0} no es material o empaque.", spec.SpecSummary.SpecNumIssueNum));
            }

            //Check if it has missing signatures
            foreach (IGSMSignatureDoc signature in spec.SpecSummary.SignatureDocuments.Values)
            {
                if (signature.ParentWorkflowStatus == spec.SpecSummary.WorkflowStatus && signature.WorkflowStatus.Status != "Approved")
                {
                    throw new CustomValidationException(string.Format("No han sido completadas todas las aprobaciones."));
                }
            }

            //MAPPING
            var item = new articuloInputArticulo();

            //COMMON ATTRIBUTES
            //6. Group
            var groupNode = spec.SpecSummary.TaxonomyNode;
            item.Group = groupNode != null ? groupNode.ExternalID : string.Empty;
            if (string.IsNullOrWhiteSpace(item.Group))
            {
                listMessages.Items.Add("Grupo es obligatorio.");
                isValid = false;
            }

            //5. Subcategory 
            var subcategoryNode = groupNode != null ? groupNode.ParentNode : null;
            item.SubCategory = subcategoryNode != null ? subcategoryNode.ExternalID : string.Empty;
            if (string.IsNullOrWhiteSpace(item.SubCategory))
            {
                listMessages.Items.Add("Subcategoria es obligatoria.");
                isValid = false;
            }

            //4. Category
            var categoryNode = subcategoryNode != null ? subcategoryNode.ParentNode : null;
            item.Category = categoryNode != null ? categoryNode.ExternalID : string.Empty;
            if (string.IsNullOrWhiteSpace(item.Category))
            {
                listMessages.Items.Add("Categoria es obligatoria.");
                isValid = false;
            }

            //1. Tipo de artículo
            item.TipoArticulo = item.Category;
            if (string.IsNullOrWhiteSpace(item.TipoArticulo))
            {
                listMessages.Items.Add("Tipo articulo es obligatorio.");
                isValid = false;
            }

            //2. Id PLM
            item.IdPLM = spec.SpecSummary.SpecNumIssueNum;
            if (string.IsNullOrWhiteSpace(item.IdPLM))
            {
                listMessages.Items.Add("Id PLM es obligatorio.");
                isValid = false;
            }

            //7. Descripción del Producto
            item.DescripcionProducto = spec.SpecSummary.FreeTextName != null ? spec.SpecSummary.FreeTextName.Name : string.Empty;
            if (string.IsNullOrWhiteSpace(item.DescripcionProducto))
            {
                listMessages.Items.Add("Descripcion de producto es obligatoria.");
                isValid = false;
            }

            foreach (ILegacyProfiles profile in spec.SpecSummary.LegacyProfiles.Values)
            {
                //20. SKU Artículo
                if (profile.LegacyProfileInfo.SystemCode == SKUSystemCode)
                {
                    item.SKUArticulo = profile.Equivalent;
                }
                //3. Codigo CHAMP
                else if (profile.LegacyProfileInfo.SystemCode == ChampSystemCode)
                {
                    item.CodigoChamp = profile.Equivalent;
                }
            }

            

            //3. Codigo CHAMP
            if (item.TipoArticulo == RawMaterials && string.IsNullOrWhiteSpace(item.CodigoChamp))
            {
                listMessages.Items.Add("Codigo Champ es obligatorio para materias primas.");
                isValid = false;
            }

            if (ReflectionHelper.HasProperty((object)spec, "AvailableUOMs"))
            {
                IXDataObjectCollection availableUOMs = ReflectionHelper.GetPropObject((object)spec, "AvailableUOMs") as IXDataObjectCollection;
                foreach (ISpecAvailUOM uom in availableUOMs.Values)
                {
                    //14. UOM primaria
                    if (string.IsNullOrEmpty(item.UOMPrimaria))
                    {
                        item.UOMPrimaria = uom.BaseUOM != null ? uom.BaseUOM.UOMML.Abbreviation.ToLower() : null;
                    }
                    else
                    {
                        //15. UOM Dual
                        item.UOMDUal = uom.CurrentUOM != null ? uom.CurrentUOM.UOMML.Abbreviation.ToLower() : null;
                    }
                }
            }
            //14. UOM primaria
            if (string.IsNullOrWhiteSpace(item.UOMPrimaria))
            {
                listMessages.Items.Add("UOM primaria es obligatoria.");
                isValid = false;
            }

            //19. Organización
            if (ReflectionHelper.HasProperty((object)spec, "ApprovedUsages"))
            {
                var bus = new List<string>();
                IXDataObjectCollection aus = ReflectionHelper.GetPropObject((object)spec, "ApprovedUsages") as IXDataObjectCollection;
                if (aus != null)
                {
                    foreach (IApprovedUsage au in aus.Values)
                    {
                        foreach (IBusinessUnitDO bu in au.BusinessUnits.Values)
                        {
                            bus.Add(bu.FreeTextName.Name);
                        }
                    }
                }
                item.Organizacion = string.Join(",", bus.ToArray());
            }
            if (string.IsNullOrWhiteSpace(item.Organizacion))
            {
                listMessages.Items.Add("Organización es obligatoria.");
                isValid = false;
            }

            //21. New/Update Flag
            item.NewOrUpdateFlag = item.SKUArticulo != null ? "U" : "N";

            var extAtt = spec is IIngredientSpecification ? "ExtendedAttributes" : "EquipmentAttributes";
            if (ReflectionHelper.HasProperty((object)spec, extAtt))
            {
                IXDataObjectCollection eas = ReflectionHelper.GetPropObject((object)spec, extAtt) as IXDataObjectCollection;

                foreach (IExtendedAttributeBaseDO ea in eas.Values)
                {
                    //22. Categoría SAT
                    if (ea.ExtendedAttributeType.AttributeID == CategoriaSATAttrId)
                    {
                        item.CategoriaSAT = Utils.GetEAValue(ea);
                    }
                    //23. Fecha Lanzamiento
                    else if (ea.ExtendedAttributeType.AttributeID == FechaLanzamientoAttrId)
                    {
                        item.FechaLanzamiento = Utils.GetEAValue(ea);
                    }
                    //Add 22-03-2019
                    //24. Código de producto terminado
                    else if (ea.ExtendedAttributeType.AttributeID == CodProdTermAttrId)
                    {
                        item.Futuro1 = Utils.GetEAValue(ea);
                    }
                }
            }

      

 //Funciona correctamente       
            
           

            //24 Futuro1 Código de producto terminado
            if (item.TipoArticulo == BulkCategory && string.IsNullOrWhiteSpace(item.Futuro1))
            {
                listMessages.Items.Add("El campo Código de Producto Terminado es obligatorio.");
                isValid = false;
            }
            //listMessages.Items.Add("Futuro 1: " + item.Futuro1); 

///////////////////////

            //MATERIAL SPECIFIC ATTRIBUTES
            if (spec is IIngredientSpecification)
            {
                var material = (IIngredientSpecification)spec;
                
                //8. Densidad
                item.Densidad = (decimal)material.IngredientAttributes.MassDensity;
                var countries_name = new List<string>();    
               foreach (ICountry country  in material.IngredientAttributes.Countries.Values){
                   //item.Futuro2 = country.CountryName;
                   countries_name.Add(country.CountryName);
                    
                }
                item.Futuro2 = string.Join(",", countries_name.ToArray());
                
                //25. Futuro2 Country of Origin ADD CCR 25/03/2019
                //Eliminar al finalizar
              
                if (item.TipoArticulo == RawMaterials && string.IsNullOrWhiteSpace(item.Futuro2))
                {
                    listMessages.Items.Add("El campo Country of Origin es obligatorio.");
                    isValid = false;
                }
                 
                if (item.Densidad <= 0)
                {
                    listMessages.Items.Add("Densidad es obligatoria.");
                    isValid = false;
                }

                //9. Permanencia en estante
                double shelflife = 0;
                foreach (IGSMShelfLifeDO sl in material.IngredientAttributes.ShelfLives.Values)
                {
                    var value = Math.Min(shelflife, sl.InternalShelfLifeValue) > 0 ? Math.Min(shelflife, sl.InternalShelfLifeValue) : Math.Max(shelflife, sl.InternalShelfLifeValue);
                    value = Math.Min(value, sl.SuppliersShelfLifeValue) > 0 ? Math.Min(value, sl.SuppliersShelfLifeValue) : Math.Max(value, sl.SuppliersShelfLifeValue);
                    value = Math.Min(value, sl.MinimumDaysRemainingValue) > 0 ? Math.Min(value, sl.MinimumDaysRemainingValue) : Math.Max(value, sl.MinimumDaysRemainingValue);
                    shelflife = value;
                }

                   
                item.PermanenciaEstante = (decimal)shelflife;
                if (item.PermanenciaEstante <= 0)
                {
                    listMessages.Items.Add("Permanencia en estante es obligatoria.");
                    isValid = false;
                }

                //16. UOM Origen
                item.UOMOrigen = material.IngredientAttributes.MassUom != null ? material.IngredientAttributes.MassUom.UOMML.Abbreviation.ToLower() : null;
                if (string.IsNullOrWhiteSpace(item.UOMOrigen))
                {
                    listMessages.Items.Add("UOM origen es obligatoria.");
                    isValid = false;
                }

                //17. UOM Destino
                item.UOMDestino = material.IngredientAttributes.VolumeUOM != null ? material.IngredientAttributes.VolumeUOM.UOMML.Abbreviation.ToLower() : null;
                if (string.IsNullOrWhiteSpace(item.UOMDestino))
                {
                    listMessages.Items.Add("UOM destino es obligatoria.");
                    isValid = false;
                }

                //18. Conversión
                item.Conversion = item.Densidad;
                if (item.Conversion <= 0)
                {
                    listMessages.Items.Add("Conversión es obligatoria.");
                    isValid = false;
                }

            }
            //PACKAGING SPECS ATTRIBUTES
            else if (spec is IPackagingSpecification)
            {
                var pack = (IPackagingSpecification)spec;
                
                foreach (IExtendedAttributeSectionInstanceDO cs in pack.ExtendedAttributeSections.Values)
                {
                    foreach (IExtendedAttributeRowInstanceDO row in cs.Rows.Values)
                    {
                        foreach (IExtendedAttributeSectionCellInstanceDO cell in row.Cells.Values)
                        {
                            var ea = cell.ExtendedAttribute;
                            //10. Altura
                            if (ea.ExtendedAttributeType.AttributeID == AlturaAttrId)
                            {
                                item.Altura = (decimal)((IExtendedAttributeNumericDO)ea).AttributeValue;
                            }
                            //11. Ancho
                            else if (ea.ExtendedAttributeType.AttributeID == AnchoAttrId)
                            {
                                item.Ancho = (decimal)((IExtendedAttributeNumericDO)ea).AttributeValue;
                            }
                            //12. Longitud
                            else if (ea.ExtendedAttributeType.AttributeID == LongitudAttrId)
                            {
                                item.Longitud = (decimal)((IExtendedAttributeNumericDO)ea).AttributeValue;
                            }
                            //13. Peso
                            else if (ea.ExtendedAttributeType.AttributeID == PesoAttrId)
                            {
                                item.Peso = (decimal)((IExtendedAttributeNumericDO)ea).AttributeValue;
                            }
                           
                        }
                    }
                } 
               
            }

            if (!isValid)
            {
                return false;
            }

            foreach (var prop in item.GetType().GetProperties())
            {
                if (prop.Name.Contains("Specified"))
                {
                    var valueProp = item.GetType().GetProperty(prop.Name.Replace("Specified", string.Empty));
                    if (valueProp != null) prop.SetValue(item, valueProp.GetValue(item) != null);
                }
            }

            //Call MDM Web Service
            Logger.Log(Utils.XmlSerialize(item));
            var client = ServiceClientHelper.GetClient((string)Utils.GetConfigurationValue("string(/configuration/constants/endpoint/text())"));
            var output = client.process(new articuloInput() { Articulo = item });
            Logger.Log(Utils.XmlSerialize(output.RespuestaMDM));
            
            if (output.RespuestaMDM.Estatus != "S")
            {
                throw new CustomValidationException(output.RespuestaMDM.DescripcionError);
            }
            
            Utils.SaveIntegrationStatus(spec.PKID, spec.SpecSummary.WorkflowStatus.Status, output.RespuestaMDM.Estatus);

            //Save SKU
            var sku = output.RespuestaMDM.SKU;
            
           
            ILegacyProfile skuProfile = Utils.GetLegacyProfile(SKUSystemCode);
            ILegacyProfiles skuCrossReference = null;
            specService.Edit();
            var profiles = AppPlatformHelper.DataManager.newCollection(EnumCollectionType.LINKED_LIST);
            if (ReflectionHelper.HasProperty((object)spec, "LegacyProfiles"))
            {
                profiles = ReflectionHelper.GetPropObject((object)spec, "LegacyProfiles") as IXDataObjectCollection;
                foreach (ILegacyProfiles cr in profiles.Values)
                {
                    if (cr.LegacyProfileInfo.SystemCode == skuProfile.SystemCode)
                    {
                        skuCrossReference = cr;
                        skuCrossReference.Equivalent = sku;
                    }
                }
                if (skuCrossReference == null)
                {
                    skuCrossReference = AppPlatformHelper.DataManager.newObject((EnumDataObject)EnumDataObjectTypes.LegacyProfiles) as ILegacyProfiles;
                    skuCrossReference.LegacyProfileInfo = skuProfile;
                    skuCrossReference.ExtManaged = skuProfile.ExtManagedDefault;
                    skuCrossReference.Equivalent = sku;
                    profiles.Add(skuCrossReference);
                }
            }
            spec.SpecSummary.ShortNameML.Name = sku;
            spec.SpecSummary.FreeTextName.Name = output.RespuestaMDM.DescripcionError;
            specService.Save(false);

            listMessages.Items.Add("Item enviado a MDM.");

            return true;
        }

        private void GetConstants()
        {
            try
            {
                SKUSystemCode = (string)Utils.GetConfigurationValue("string(/configuration/constants/skuSystemCode/text())");
                ChampSystemCode = (string)Utils.GetConfigurationValue("string(/configuration/constants/champSystemCode/text())");
                CategoriaSATAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/categoriaSATAttrId/text())");
                FechaLanzamientoAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/fechaLanzamientoAttrId/text())");
                AlturaAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/alturaAttrId/text())");
                AnchoAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/anchoAttrId/text())");
                LongitudAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/longitudAttrId/text())");
                PesoAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/pesoAttrId/text())");
                RawMaterials = (string)Utils.GetConfigurationValue("string(/configuration/constants/rawMaterialsCategory/text())");
                BulkCategory = (string)Utils.GetConfigurationValue("string(/configuration/constants/bulkCategory/text())");
                CodProdTermAttrId = (string)Utils.GetConfigurationValue("string(/configuration/constants/codProdTermAttrId/text())");
            }
            catch (Exception e)
            {
                Logger.Log("GetConstants: " + e.ToString());
            }
        }
            
    }
}