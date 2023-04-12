
# QuickPay Payment Window

The QuickPay Payment Window checkout handler is designed to work with QuickPay v10 – released in 2015 – which supports Danske Bank's Mobile Pay Online and embedded payment windows.

## Some things to note

This provider can only be tested on a public URL

While QuickPay can handle transaction fees & returns, these features are handled natively in Dynamicweb and are not a part of the integration

The QuickPay v10 platform does not have a dedicated test mode – so all tests happen with your regular account and a number of test cards. You may also find this list of error codes useful.

If you don’t have a QuickPay account you can sign up for a free test account – this comes with standard system users called Payment Window, API User, and the Owner account. The Owner has all permissions set, but for production you should create a dedicated user or use the Payment Window system user.

To configure the QuickPay Payment Window checkout handler in Dynamicweb you need the following 4 keys:

 - Merchant ID 
 - API Key (Payment window) 
 - Agreement ID (Payment window)
 - Private Key 
 
These can be found  under `Settings > Integration` in the QuickPay Manager interface. The Merchant ID and Private key are tied to the account, whereas the Agreement ID and API key are tied to a user.

When you have the four keys you can configure the checkout handler:

Go to `Settings > Ecommerce > Orders > Payments` and create a new payment method
Select the **QuickPay Payment Window** checkout handler
Fill in the parameters section
