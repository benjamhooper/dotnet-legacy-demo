<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="MoveOut.aspx.cs" Inherits="Tallgrass.CustomerPortal.Classic.MoveOut" %>
<html>
<body>
  <form id="form1" runat="server">
    <asp:Calendar ID="MoveOutCalendar" runat="server" />
    <asp:Button ID="SubmitButton" runat="server" Text="Schedule move-out" OnClick="SubmitButton_Click" />
    <asp:Label ID="EstimateLabel" runat="server" />
  </form>
</body>
</html>
