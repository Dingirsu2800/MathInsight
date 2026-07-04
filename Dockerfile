FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY MathInsight.sln ./
COPY src/MathInsight.WebAPI/MathInsight.WebAPI.csproj src/MathInsight.WebAPI/
COPY src/MathInsight.Shared/MathInsight.Shared.csproj src/MathInsight.Shared/
COPY src/MathInsight.Modules.Identity_Access/MathInsight.Modules.Identity_Access.csproj src/MathInsight.Modules.Identity_Access/
COPY src/MathInsight.Modules.QuestionBank/MathInsight.Modules.QuestionBank.csproj src/MathInsight.Modules.QuestionBank/
COPY src/MathInsight.Modules.Testing/MathInsight.Modules.Testing.csproj src/MathInsight.Modules.Testing/
COPY src/MathInsight.Modules.Grading_Analytics/MathInsight.Modules.Grading_Analytics.csproj src/MathInsight.Modules.Grading_Analytics/
COPY src/MathInsight.Modules.Recommender/MathInsight.Modules.Recommender.csproj src/MathInsight.Modules.Recommender/
COPY src/MathInsight.Modules.Gamification/MathInsight.Modules.Gamification.csproj src/MathInsight.Modules.Gamification/
COPY src/MathInsight.Modules.Learning_Lecture/MathInsight.Modules.Learning_Lecture.csproj src/MathInsight.Modules.Learning_Lecture/
COPY src/MathInsight.Modules.Notification_Report/MathInsight.Modules.Notification_Report.csproj src/MathInsight.Modules.Notification_Report/
COPY src/MathInsight.Modules.TestGen/MathInsight.Modules.TestGen.csproj src/MathInsight.Modules.TestGen/

RUN dotnet restore src/MathInsight.WebAPI/MathInsight.WebAPI.csproj

COPY . .
RUN dotnet publish src/MathInsight.WebAPI/MathInsight.WebAPI.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MathInsight.WebAPI.dll"]
