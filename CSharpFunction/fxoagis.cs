using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http; 
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using fxoagis.azure.io;


namespace fxoagis
{


    public class Functions
    {
        static azureDocumentDB db;
        //0-shipmentident, 1-retailerident
        //
        //  in V2 documentReference.identifier.content changes to shipmentReference.identifier 
        //
        const string sqlSetupFiles = @"SELECT c.shippedItemInstance FROM c where c.shippedItemInstance[0].party[1].location.glnid='{1}'  AND c.shippedItemInstance[0].documentReference.identifier.content= '{0}'";
        const string sqlCosmoDocumentIDs = @"SELECT value c.id FROM c where c.shippedItemInstance[0].party[1].location.glnid='{1}'  AND c.shippedItemInstance[0].documentReference.identifier.content= '{0}'";
        const string sqlSetupFileDirect = @"SELECT c.shippedItemInstance FROM c where c.id = '{0}'";

        [FunctionName("postoagisdocument")]
        public static async Task<IActionResult> postoagisdocument(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "shippediteminstance")] HttpRequest req, ILogger log)
        {
            var id = Guid.NewGuid().ToString();
            dynamic data = null;


            try
            {
                using var sr = new StreamReader(req.Body);
                string requestBody = await sr.ReadToEndAsync();

                data = JsonConvert.DeserializeObject(requestBody);
        //
        //  in V2 documentReference.identifier.content changes to shipmentReference.identifier 
        //
                var shipmentid = (string)data.shippedItemInstance[0].documentReference.identifier.content;
                var retailerid = (string)data.shippedItemInstance[0].party[1].location.glnid;

                var sql = string.Format(sqlCosmoDocumentIDs, shipmentid, retailerid);


                if (db == null)
                    db = azureDocumentDB.connectDB("DB", "shippediteminstance", "shippediteminstance");

                db?.ResetIfExpired();

                var rs = await db?.queryDocuments<dynamic>(sql);

                if (rs.Count == 0)
                {
                    db?.createDocument(data, id);
                    return new JsonResult(id);
                }

                return new JsonResult(new { id, error = true, errorMsg = $"duplicates detected: {rs.Count}, document not stored", data }) { StatusCode = StatusCodes.Status400BadRequest };
            }
            catch (Exception ex)
            {
                return new JsonResult(new { id, error = true, errorMsg = ex.Message, data }) { StatusCode = StatusCodes.Status500InternalServerError };
            }




        }

        [FunctionName("putoagisdocument")]
        public static async Task<IActionResult> putoagisdocument(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "shippediteminstance/id")] HttpRequest req, ILogger log,string id)
        {
            
            dynamic data = null;




            try
            {
                if (Guid.TryParse(id, out _) == false)
                {
                    return new JsonResult(new { id, error = true, errorMsg = "Supplied id is not a guid", data }) { StatusCode = StatusCodes.Status406NotAcceptable };
                }

                using var sr = new StreamReader(req.Body);
                string requestBody = await sr.ReadToEndAsync();

                data = JsonConvert.DeserializeObject(requestBody);
        //
        //  in V2 documentReference.identifier.content changes to shipmentReference.identifier 
        //
                var shipmentid = (string)data.shippedItemInstance[0].documentReference.identifier.content;
                var retailerid = (string)data.shippedItemInstance[0].party[1].location.glnid;

                var sql = string.Format(sqlCosmoDocumentIDs, shipmentid, retailerid);


                if (db == null)
                    db = azureDocumentDB.connectDB("DB", "shippediteminstance", "shippediteminstance");

                db?.ResetIfExpired();

                var rs = await db?.queryDocuments<dynamic>(sql);

                if (rs.Count == 0)
                {
                    await db?.createDocument(data, id);
                    return new JsonResult(id);
                }
                else if (rs.Count == 1)
                {
                    id = rs[0];
                    await db?.writeDocument(data, id);
                    return new JsonResult(id);
                }
                else
                {
                    foreach (var r in rs)
                    {
                        var rid = (string)r;

                        await db?.deleteDocument<dynamic>(rid, rid);
                    }

                    await db?.createDocument(data, id);
                    return new JsonResult(id);
                }



            }
            catch (Exception ex)
            {
                return new JsonResult(new { id, error = true, errorMsg = ex.Message, data }) { StatusCode = StatusCodes.Status500InternalServerError };
            }



        }

        //This may have uninteded issues due to the elements in the array being stripped out.
        //SELECT  value c.shippedItemInstance[0] FROM c where c.id = {theid}

        [FunctionName("getoagisdocument")]
        public static async Task<IActionResult> getoagisdocument(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shippediteminstance/{id}")] HttpRequest req,
         ILogger log, string id)
        {

            if (db == null)
                db = azureDocumentDB.connectDB("DB", "shippediteminstance", "shippediteminstance");


            // SELECT  * FROM c where c.id = "05b9e877-01ea-40f4-ab35-08102c57f9e4"


            db?.ResetIfExpired();

            var data = await db?.readDocument<dynamic>(id, "id)");

            return
                new JsonResult(new { error = true, errorMsg = "Function not supported" })
                { StatusCode = StatusCodes.Status500InternalServerError };
        }



        [FunctionName("setupfiles")]
        public async Task<IActionResult> getsetup(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "setupfiles"),] HttpRequest req, ExecutionContext executionContext
            , ILogger log)
        {

            bool isoOut = false;
            bool admOut = false;

            string shipmentident = string.Empty;
            string retailerident = string.Empty;

            Microsoft.Extensions.Primitives.StringValues contentHeaders = default;
            

            try {


                if (req.Headers.TryGetValue("content-type", out contentHeaders))
                {
                    //Determine output type from header
                    if (contentHeaders.Contains("application/vnd.aggateway.adapt.iso+zip"))
                    {
                        isoOut = true;
                    }
                    else if (contentHeaders.Contains("application/vnd.aggateway.adapt.adm+zip"))
                    {
                        admOut = true;
                    }
                    else
                    {
                        return new JsonResult(new { error = true, errorMsg = "Media types expected: application/vnd.aggateway.adapt.iso+zip, application / vnd.aggateway.adapt.adm + zip" }) { StatusCode = StatusCodes.Status415UnsupportedMediaType };
                    }


                    try
                    {
                        shipmentident = req.Query["shipment.identifier"].First();
                        retailerident = req.Query["retailer.identifier"].First();

                        if (shipmentident.Length == 0 || retailerident.Length == 0)
                        {
                            //missing needed query parameters
                            return new JsonResult(new { error = true, errorMsg = "Mandatory query parameters are shipment.identifier and retailer.identifier" }) { StatusCode = StatusCodes.Status428PreconditionRequired };
                        }
                    }
                    catch
                    {
                        return new JsonResult(new { error = true, errorMsg = "Mandatory query parameters are shipment.identifier and retailer.identifier" }) { StatusCode = StatusCodes.Status428PreconditionRequired };

                    }


                    if (db == null)
                        db = azureDocumentDB.connectDB("DB", "shippediteminstance", "shippediteminstance");

                    db?.ResetIfExpired();

                    //get document from cosmos, not http request
                    var sql = string.Format(sqlSetupFiles, shipmentident, retailerident);
                    var rs = await db?.queryDocuments<dynamic>(sql);

                    if (rs?.Count == 0)
                    {
                        //return furnction right now - error, document not found
                        return new JsonResult(new { error = true, errorMsg = $"Resource not found for shipment.identifier={shipmentident}, retailer.identifier={retailerident}" }) { StatusCode = StatusCodes.Status404NotFound };

                    }

                    //rs (record set) should only have 1 element.  First one in the list is used. Changing rs[0].ToString() to rs.ToString() will create a multi-document.
                    
                    string jobj = JsonConvert.SerializeObject(rs[0]).ToString();
                    string input = jobj.Replace("\r\n", string.Empty).Replace("\t", string.Empty).Replace(" ", string.Empty); //Remove whitespace in the request as desired


                    //Override the default location of the ADAPT resource files to accomodate placement within Azure function   
                    AgGateway.ADAPT.Representation.UnitSystem.UnitSystemManager.UnitSystemDataLocation = System.IO.Path.Combine(executionContext.FunctionDirectory, "../Resources", "UnitSystem.xml");
                    AgGateway.ADAPT.Representation.RepresentationSystem.RepresentationManager.RepresentationSystemDataLocation = System.IO.Path.Combine(executionContext.FunctionDirectory, "../Resources", "RepresentationSystem.xml");
                    AgGateway.ADAPT.ISOv4Plugin.Representation.DdiLoader.DDIDataFile = System.IO.Path.Combine(executionContext.FunctionDirectory, "../Resources", "ddiExport.txt");
                    AgGateway.ADAPT.ISOv4Plugin.Representation.IsoUnitOfMeasureList.ISOUOMDataFile = System.IO.Path.Combine(executionContext.FunctionDirectory, "../Resources", "IsoUnitOfMeasure.xml");

                    //Write the input to a file so that the ShippedItemInstance plugin can read it
                    string folder = System.IO.Path.GetTempPath();
                    string tempPath = Path.Combine(folder, "input.json");
                    File.WriteAllText(tempPath, input);

                    //Read the input data
                    //
                    //  in V2 AgGateway.ADAPT.ShippedItemInstancePlugin.Plugin changes to AgGateway.ADAPT.ShippedItemInstanceV2Plugin.Plugin 
                    //
                    AgGateway.ADAPT.ShippedItemInstancePlugin.Plugin p = new AgGateway.ADAPT.ShippedItemInstancePlugin.Plugin();
                    var admList = p.Import(folder);

                    if (admList.Count == 1) //We assume the caller is requesting one document at a time
                    {
                        var inputData = admList[0];

                        string outputPath = Path.Combine(folder, "output");
                        if (Directory.Exists(outputPath))
                        {
                            Directory.Delete(outputPath, true);
                        }
                        Directory.CreateDirectory(outputPath);

                        string outputZip = Path.Combine(folder, "output.zip");
                        if (File.Exists(outputZip))
                        {
                            File.Delete(outputZip);
                        }

                        if (isoOut)
                        {
                            AgGateway.ADAPT.ISOv4Plugin.Plugin isoPlugin = new AgGateway.ADAPT.ISOv4Plugin.Plugin();
                            isoPlugin.Export(inputData, outputPath, new AgGateway.ADAPT.ApplicationDataModel.ADM.Properties());
                        }
                        else if (admOut)
                        {
                            AgGateway.ADAPT.ADMPlugin.Plugin admPlugin = new AgGateway.ADAPT.ADMPlugin.Plugin();
                            admPlugin.Export(inputData, outputPath);
                        }

                        System.IO.Compression.ZipFile.CreateFromDirectory(outputPath, outputZip);
                        return new FileContentResult(File.ReadAllBytes(outputZip), "application/octet-stream");

                    }
                    else //Multiple documents not supported at this time.  Otherwise input not valid.
                    {
                        return new JsonResult(new { error = true, errorMsg = "Multiple documents exist and currently unsupported." }) { StatusCode = StatusCodes.Status416RequestedRangeNotSatisfiable };

                    }
                }
                else
                    return new JsonResult(new { error = true, errorMsg = "Content-Type header missing.  Media types expected: application/vnd.aggateway.adapt.iso+zip, application/vnd.aggateway.adapt.adm+zip" }) { StatusCode = StatusCodes.Status415UnsupportedMediaType };
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Either internal cosmo error or could not access temp files for processing: shipment.identifier={shipmentident}, retailer.identifier={retailerident},contentHeaders={string.Join(",", contentHeaders.ToArray())}");
                return new JsonResult(new { error = true, errorMsg = "Internal error has occured.  Request could not be processed at this time" }) { StatusCode = StatusCodes.Status500InternalServerError };
                //log error
            }





        }
    }


}


/*
   This will return an array of objects below the shippedItemInstance[] as the data looks to have arrays of a single 
   
   In V2, documentReference.identifier.content becomes shipmentReference.identifier
   
   SELECT  value c.shippedItemInstance[0] FROM c where c.shippedItemInstance[0].documentReference.documentDateTime = "20210202"
  
  
 */
