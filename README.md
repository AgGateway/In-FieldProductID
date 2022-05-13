# In-Field Product Identification
This GitHub repo provides the Azure resources to support in-field product identification (agriculture).  The components within the Green system node in the deployment diagram below reside in this GitHub repository.  

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
The Dispensing Work Record is sent back from the process control system to the Sales Order system with the shipping information including Bill of Lading images, and composition.

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
