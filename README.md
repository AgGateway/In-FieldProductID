# In-Field Product Identification Overview
This GitHub repo provides the Azure resources to supports the agricultural use case to improve product identification capabilities in a farmer's field (in-field product identification), focusing on actual product received by the farmer.  

V1 allowed the Retailer to send information about the actual seed product shipped to the Farmer, including shipment identifer, product identifiers, seed lot id, and seed treatment.   
V2 allows the Retailer to sent Crop Nutrition and Crop Protection composition details, and improves the means to represent seed treatment and the shipment reference information.
Changes V3-V4:
1) Party -> shipToParty (grower) and shipFromParty (Retailer), etc.
2) Attachment -> item.relatedIds -- URI reference to AGIIS
3) Quantity removed from Substance (Not realistic)
4) Lot flattened
5) Shipment Reference now includes shipTo and shipFrom parties, receipt date at the farm, or pickup date at the retailer, and received, accepted, rejected, and return quantities and RMA identifier.
6) Simplified the Results.Quantitative.Measurement[] structure, flattening measurement

This also allows the Farmer to leverage either a variety of tools such as a mobile application or a Farm Management Information System (FMIS), as well as the Farmer's OEM equipment manufacturer application (e.g., Deere Operations Center, AGCO Fuse, CNH AFS, etc.) to retrieve the Product shipped by a Retailer in the form of setup files.  

It must be clear that an OEM Platform would send to the display _essential_ identifiers needed for correlation and product instance identification.  The OEM platform would store _non-essential_ identifiers related to the product instance on their respective cloud platforms, and allow these identifiers to be referenced back via key data elements, specifically identifiers, that are captured in the work records as a result of product identification.  Essential identifiers that are useful for product identification include product id (e.g., GTIN, name), Seed Lot Identifier (optional, but ideal for germination challenges), and Seed Treatment (optional, but ideal when available, especially if prescribed by an agronomist to address nematodes or other challenges observed in the field, or for comparative analysis).  Non-essential identifiers include seed treatment, crop nutrition, and crop protection composition details including EPA registrion identifiers and CAS identifiers for active ingredients.

It is acknowledged that many capabilities do not exist on many of the platforms used by farmers and especially older displays that rely on planting prescriptions including rate information, etc.  The use of mobile applications may be warranted to provide supplemental capabilities.  The proof-of-concept in 2021 and the pilot in 2022 proved the feasibility of loading product information, with older displays have some challenges due to display length (see Issue #9).  

What IS encouraging is the ability of the Retailer ERP systems to digitize the delivery document in the Shipped Item Instance JSON format.  Other than the seed treatment composition (e.g., EPA, CAS ids), this information is already avaialable on existing paper-based delivery documents.  The integration using the OpenAPI allows the generation of a QR code that can be included on the delivery document provided to a driver, allowing the farmer to retrieve the shipment detail once logged into the platform of choice.  The number of shipment lines on a delivery document for seed product, even accounting for the existing seed lot detail _currently available_ today on these documents, is typically 5-20 lines.  

Future work aims to add further efficiencies to provide auto-identification capabilities including BLE beacons, Data Matrix barcoding (product, lot/batch, etc), and/ or RFID tags on bags and seed boxes for use with tenders.  We aim, over time, to avoid having the farmer enter into the cab to select the product.  Solutions could leverage Wi-Fi on a tractor displays, future High-Speed ISOBUS over Ethernet connections, and so forth.

# API Implementation Details
The components within the Green system node in the deployment diagram below reside in this GitHub repository.  

The solution provides a three tier architecture including authentization/authorization, API logic, and persistent storage.  There are four types of components:

* API Management to provide a security layer for access to the Azure unction and Logic App:
  * each verb/ resource has a specific XML policy
  * the all proxy provides the configuration to the Identity Provider to validate the token generated based on the client id and secret provided by the IdP
* Azure C# function
  * Provides the ability to POST (insert) the ShippedItemInstance JSON in Cosmos, first creating an /id with the SDK then adding the document
  * Provides the ability to PUT (replace) the ShippedItemInstance JSON in Cosmos based on the /id from the POST endpoint
  * Provides the ability to DELETE (remove) the ShippedItemInstance JSON in Cosmos based on the /id from the POST endpoint
  * Provides the ability to GET /setupfiles based on the content-Type HTTP header, returning either the ADAPT ADM.zip or ISOXML zip file as octet-stream.
* Logic App
  * Provides the ability GET the original JSON for Farm Management Information Systems that want to receive the product into inventory at the time of receipt
* Cosmos DB
  * Provides the persistent storage of the Shipped Item Instance JSON payload

![In-Field Product Identification](https://github.com/AgGateway/In-FieldProductID/blob/main/Documentation/InFieldProductIDDeploymentDiagram.png)

## Seed User Story
The farmer places a seed order with an Ag Retailer.   
At the time of shipment, the seed is gathered including the seed lot identifier and other identifying information such as seed treatement with EPA registration identifier for the primary active ingredient.

## Crop Nutrition User Story
The farmer places a fertilizer order with an Ag Retailer.   
At the time of shipment, the fertilizer is dispensed into a shipping container such as a tank, including the identifiers such as the shipping container and the dispensing ticket identifier.
The Dispensing Work Record is sent back from the process control system to the Sales Order system with the shipping information including Bill of Lading images, and actual composition.  The work order can be the batch identifier in many process control systems.

## Common Implementation
A shipping document is provided to the carrier, which includes a QR code the encodes the URL to retreive the shipment information.  
The QR code URL includes the host, resource path, shipment.identifier and the retailer.identifier (GLN).  Their acccount number is not needed on the API call.

When a farmer receives the shipment, the printed document is provided to the farmer by the carrier / driver.
The farmer logs into their Farm Implement Cloud Platform, selects the option to load the received product into the tractor display.
The farmer scans the QR code on the shipping document.
The Farm Implement Cloud Platform sets the content-type to required format required (ADM or ISO), and calls the API.
The API returns an octet-stream of the zip file, which is saves as a file on the  Farm Implement Cloud Platform.
The Farm Implement Cloud Platform asks if the farmer would like to download the products to the tractor display now, or at a later point in time.

The following sequence diagram illustrates a typical implementation of the parties illustrated in the deploymentation

![image](https://github.com/AgGateway/In-FieldProductID/blob/main/Documentation/AgGateway%20Shipped%20Item%20Instance.png)

## In-Field Product Identification
The product is loaded in the tractor display prior to the field operation.

The farmer will select the appropriate product from the list prior to execution the actual field operation, either manual selection from a mobile app connected to the display, from the display itself, or through auto-identification such as BLE beacons, RFID tags, or barcodes on the seed tags themselves.  This is dependent on the implementation provided by the retailer and the Farm Implement Cloud Platform.


## Opportunity for Innovation - Node-RED Example
Since Node-RED can be run on a Raspberry Pi and pre-loaded in Raspberry Pi OS 64, it can be a catalyst for future innovation when complimented with BLE Beacon identification, add-on Camera for barcode reading, or add-on RFID tag readers.

![image](https://github.com/AgGateway/In-FieldProductID/blob/main/Node-RED_Example/Node-RED_Example.JPG)

Node-RED also comes with a dashboard component that can display the JSON in a table, or add other components like pie charts, etc.
https://flows.nodered.org/node/node-red-dashboard
