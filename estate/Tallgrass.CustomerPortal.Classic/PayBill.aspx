<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="PayBill.aspx.cs" Inherits="Tallgrass.CustomerPortal.Classic.PayBill" %>
<html>
<body>
  <form id="form1" runat="server">
    <asp:Label ID="AccountLabel" runat="server" />
    <asp:TextBox ID="AmountTextBox" runat="server" />
    <asp:Button ID="PayButton" runat="server" Text="Pay now" OnClick="PayButton_Click" />
    <asp:Label ID="ResultLabel" runat="server" />
  </form>
</body>
</html>
