namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class HomePageSelectors
{
    public static class Popover
    {
        public const string PopoverContainer = ".popover-container";
        public static string CloseButton => $"{PopoverContainer} {SelectorCreator.DataTestIdSelector("popover-close")}";
    }

    public static class NewNetworkInfoPopover
    {
        public const string GotItButton = ".new-network-info__wrapper button.btn-primary";
    }
    
    public static class NetworkDropdown
    {
        public static string NetworksDropdownButton => SelectorCreator.DataTestIdSelector("sort-by-networks");

        public static string CurrentNetwork => $"{NetworksDropdownButton} p";
    }
    
    public static class AccountMenu
    {
        public static string AccountButton => SelectorCreator.DataTestIdSelector("account-menu-icon");
        /// <summary>
        /// The dialog element of the account-menu popover. Used to scope child queries so that
        /// selectors like <see cref="AccountNames"/> don't match hidden off-screen DOM clones
        /// that share the same CSS class (AllAsync matches the entire page DOM).
        /// </summary>
        public const string AccountMenuPopover = ".multichain-account-menu-popover__dialog";
        public static string AddAccountOrWalletButton => SelectorCreator.DataTestIdSelector("multichain-account-menu-popover-action-button");
        public static string ImportWithPrivateKeyButton => SelectorCreator.DataTestIdSelector("multichain-account-menu-popover-add-imported-account");
        public static string PrivateKeyInput => SelectorCreator.IdSelector("private-key-box");
        public static string ConfirmImportButton => SelectorCreator.DataTestIdSelector("import-account-confirm-button");
        public static string ImportAccountError => ".mm-modal-content__dialog .mm-box--color-error-default";
        public static string AccountNames => ".multichain-account-list-item__account-name";
        public static string SearchBarInput => $"{SelectorCreator.DataTestIdSelector("multichain-account-menu-search-bar")} input";
    }
    
    public static string CopyAccountAddressButton => SelectorCreator.DataTestIdSelector("app-header-copy-button");
    
    public static string AccountOptionsMenuButton => SelectorCreator.DataTestIdSelector("account-options-menu-button");
    public static string GlobalMenuLockButton => SelectorCreator.DataTestIdSelector("global-menu-lock");
}