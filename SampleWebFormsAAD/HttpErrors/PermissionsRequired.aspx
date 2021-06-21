<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="PermissionsRequired.aspx.cs" Inherits="SampleWebFormsAAD.HttpErrors.PermissionsRequired" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            Azure Exception: Need Admin/User Consent :
            Error : <asp:Label ID="lblError" runat="server" Text=""></asp:Label>
        </div>
    </form>
</body>
</html>
