<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="SampleWebFormsAAD._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div class="jumbotron">
        <h1>ASP.NET</h1>
        <p class="lead">ASP.NET is a free web framework for building great Web sites and Web applications using HTML, CSS, and JavaScript.</p>
        <p><a href="http://www.asp.net" class="btn btn-primary btn-lg">Learn more &raquo;</a></p>
    </div>

    <div class="row">
        
        <div class="col-md-4">
            <h2>User Claims</h2>
            <asp:ListView ID="lvList" runat="server">

        <LayoutTemplate>         
            <div id="Div1" runat="server">              
                <div ID="itemPlaceholder" runat="server">              
                </div>         
            </div>      
        </LayoutTemplate>

        <EmptyDataTemplate>         
            <div id="Div2" runat="server">              
                <div ID="itemPlaceholder" runat="server">                 
                No data was returned.             
                </div>         
            </div>      
        </EmptyDataTemplate>

        <ItemTemplate>
            <asp:Label ID="ProductNameLabel" runat="server" Text='<%# Container.DataItem %>'/>
        </ItemTemplate>

</asp:ListView>
        </div>
        
    </div>

</asp:Content>
