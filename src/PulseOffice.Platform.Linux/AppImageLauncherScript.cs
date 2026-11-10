namespace PulseOffice.Platform.Linux;

public interface IAppImageLauncherScript
{
    string Text(string pathFile);
}

public sealed class AppImageLauncherScript : IAppImageLauncherScript
{
    private const string Template = """
        #!/bin/sh
        config='@PATHFILE@'
        registered=""
        [ -f "$config" ] && registered=$(cat "$config")
        if [ -n "$registered" ] && [ -f "$registered" ]; then
          [ -x "$registered" ] || chmod +x "$registered" 2>/dev/null
          exec "$registered" "$@"
        fi

        newest() {
          for dir in "$@"; do
            [ -n "$dir" ] && [ -d "$dir" ] || continue
            found=$(ls -t "$dir"/PulseOffice*.AppImage 2>/dev/null | head -n 1)
            [ -n "$found" ] && { printf '%s\n' "$found"; return 0; }
          done
          return 1
        }

        downloads=$(xdg-user-dir DOWNLOAD 2>/dev/null)
        desktop=$(xdg-user-dir DESKTOP 2>/dev/null)
        candidate=$(newest "$HOME/Applications" "$HOME/applications" "$HOME/.local/bin" "$downloads" "$HOME/Downloads" "$desktop" "$HOME")
        if [ -n "$candidate" ]; then
          chmod +x "$candidate" 2>/dev/null
          exec "$candidate" "$@"
        fi

        case "${LC_ALL:-${LC_MESSAGES:-${LANG:-en}}}" in
          it*) message="Non trovo PulseOffice: è stato spostato o cancellato. Vuoi cercarlo?"; search="Cerca…"; cancel="Annulla"; notice="Non trovo PulseOffice. Aprilo dalla nuova posizione per aggiornare il menu." ;;
          de*) message="PulseOffice wurde nicht gefunden: Es wurde verschoben oder gelöscht. Möchten Sie es suchen?"; search="Suchen…"; cancel="Abbrechen"; notice="PulseOffice wurde nicht gefunden. Öffnen Sie es am neuen Ort, um das Menü zu aktualisieren." ;;
          es*) message="No se encuentra PulseOffice: se ha movido o eliminado. ¿Quieres buscarlo?"; search="Buscar…"; cancel="Cancelar"; notice="No se encuentra PulseOffice. Ábrelo desde su nueva ubicación para actualizar el menú." ;;
          fr*) message="PulseOffice est introuvable : il a été déplacé ou supprimé. Voulez-vous le rechercher ?"; search="Rechercher…"; cancel="Annuler"; notice="PulseOffice est introuvable. Ouvrez-le depuis son nouvel emplacement pour mettre à jour le menu." ;;
          *) message="PulseOffice cannot be found: it was moved or deleted. Do you want to look for it?"; search="Look for it…"; cancel="Cancel"; notice="PulseOffice cannot be found. Open it from its new place to update the menu." ;;
        esac

        chosen=""
        if command -v zenity >/dev/null 2>&1; then
          if zenity --question --title=PulseOffice --text="$message" --ok-label="$search" --cancel-label="$cancel" 2>/dev/null; then
            chosen=$(zenity --file-selection --title=PulseOffice --file-filter="AppImage | *.AppImage" 2>/dev/null)
          fi
        elif command -v kdialog >/dev/null 2>&1; then
          if kdialog --title PulseOffice --yes-label "$search" --no-label "$cancel" --yesno "$message" 2>/dev/null; then
            chosen=$(kdialog --title PulseOffice --getopenfilename "$HOME" "*.AppImage" 2>/dev/null)
          fi
        elif command -v notify-send >/dev/null 2>&1; then
          notify-send PulseOffice "$notice"
        fi

        if [ -n "$chosen" ] && [ -f "$chosen" ]; then
          chmod +x "$chosen" 2>/dev/null
          exec "$chosen" "$@"
        fi
        exit 1
        """;

    public string Text(string pathFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(pathFile);
        return Template.Replace("@PATHFILE@", pathFile.Replace("'", "'\\''", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
}
