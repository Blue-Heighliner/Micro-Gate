namespace BlueHeighliner.MicroGate;

/// <summary>
/// Helpers for combo boxes that list the members of an enum.
/// </summary>
internal static class ComboBoxExtensions
{
    extension(ComboBox box)
    {
        /// <summary>
        /// Lists every member of an enum and selects one.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="selected">The member to select.</param>
        public void Fill<T>(T selected)
            where T : struct, Enum
        {
            box.ItemsSource = Enum.GetValues<T>();
            box.SelectedItem = selected;
        }

        /// <summary>
        /// Gets the selected member of an enum.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <returns>The selected member, or the default member if nothing is selected.</returns>
        public T Pick<T>()
            where T : struct, Enum => box.SelectedItem is T value ? value : default;
    }
}
