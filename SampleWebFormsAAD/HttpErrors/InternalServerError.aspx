<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="InternalServerError.aspx.cs" Inherits="SampleWebFormsAAD.HttpErrors.InternalServerError" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            This is 500 - Internal Server Page
           Error : <asp:Label ID="lblError" runat="server" Text=""></asp:Label>
        </div>
    </form>
</body>
</html>
