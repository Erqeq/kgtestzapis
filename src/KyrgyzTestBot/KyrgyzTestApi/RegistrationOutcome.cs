namespace KyrgyzTestBot.KyrgyzTestApi;

public enum RegistrationOutcome
{
    /// <summary>Записан</summary>
    Registered,

    /// <summary>HTTP 409: у человека уже есть активная запись</summary>
    AlreadyRegistered,

    /// <summary>HTTP 400: место успели занять или сервер не принял данные, по ответу не различить</summary>
    Rejected,
}
